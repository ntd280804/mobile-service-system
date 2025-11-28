

using System;
using System.Collections.Generic;
using System.IO;
using Oracle.ManagedDataAccess.Client;
using MobileServiceSystem.Signing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using GroupDocs.Signature.Domain;
using WebAPI.Models.Export;
using WebAPI.Models.Import;
using WebAPI.Models.Invoice;
using WebAPI.Services.PdfTemplates;

namespace WebAPI.Services
{
    public class PdfService
    {
        private readonly SignatureService _pdfSigner;
        private readonly PdfTemplateContext _templateContext;
        private readonly IPdfTemplate<ImportStockDto> _importTemplate;
        private readonly IPdfTemplate<ExportStockDto> _exportTemplate;
        private readonly IPdfTemplate<InvoiceDto> _salesTemplate;

        public PdfService(
            SignatureService pdfSigner,
            IConfiguration configuration,
            IHostEnvironment hostEnvironment,
            IPdfTemplate<ImportStockDto> importTemplate,
            IPdfTemplate<ExportStockDto> exportTemplate,
            IPdfTemplate<InvoiceDto> salesTemplate)
        {
            _pdfSigner = pdfSigner ?? throw new ArgumentNullException(nameof(pdfSigner));
            _templateContext = BuildTemplateContext(configuration, hostEnvironment);
            _importTemplate = importTemplate;
            _exportTemplate = exportTemplate;
            _salesTemplate = salesTemplate;
        }

        public byte[] GeneratePdf<T>(T dto)
        {
            var template = ResolveTemplate<T>();
            return template.GeneratePdf(dto, _templateContext);
        }

        public byte[] GenerateImportInvoicePdf(ImportStockDto dto, string signature, byte[]? qrPngBytes, string? verifyUrl) =>
            GeneratePdf(dto);

        public byte[] GenerateExportInvoicePdf(ExportStockDto dto, string signature, byte[]? qrPngBytes, string? verifyUrl) =>
            GeneratePdf(dto);

        public byte[] GenerateInvoicePdf(InvoiceDto dto, string signature, byte[]? qrPngBytes, string? verifyUrl) =>
            GeneratePdf(dto);
		
		public byte[] GenerateImportInvoicePdfAndSignWithCertificate(
			ImportStockDto dto,
			byte[] certificatePfxBytes,
			string certificatePassword,
			Action<OracleCommand> configureUpdateProcedure,
			byte[]? qrPngBytes,
			string? verifyUrl)
		{
            return GeneratePdfAndSign(
                dto,
				certificatePfxBytes, 
				certificatePassword,
                "APP.UPDATE_STOCKIN_PDF",
                configureUpdateProcedure,
                x => x.Items?.Count ?? 0);
        }
        
        public byte[] GenerateExportInvoicePdfAndSignWithCertificate(
            ExportStockDto dto,
            byte[] certificatePfxBytes,
            string certificatePassword,
            Action<OracleCommand> configureUpdateProcedure,
            byte[]? qrPngBytes,
            string? verifyUrl)
        {
            return GeneratePdfAndSign(
                dto,
                certificatePfxBytes, 
                certificatePassword,
                "APP.UPDATE_STOCKOUT_PDF",
                configureUpdateProcedure,
                x => x.Items?.Count ?? 0);
                                }
        
        public byte[] GenerateInvoicePdfAndSignWithCertificate(
            InvoiceDto dto,
            byte[] certificatePfxBytes,
            string certificatePassword,
            Action<OracleCommand> configureUpdateProcedure,
            byte[]? qrPngBytes,
            string? verifyUrl)
        {
            return GeneratePdfAndSign(
                dto,
                certificatePfxBytes,
                certificatePassword,
                "APP.UPDATE_INVOICE_PDF",
                configureUpdateProcedure,
                x => x.Items?.Count ?? 0,
                x => x.Services?.Count ?? 0);
        }

        public byte[] GeneratePdfAndSign<T>(
            T dto,
            byte[] certificatePfxBytes,
            string certificatePassword,
            string updateProcedureName,
            Action<OracleCommand> configureUpdateProcedure,
            Func<T, int> itemCountSelector,
            Func<T, int>? serviceCountSelector = null)
        {
            return GenerateAndSignPdf(
                () => GeneratePdf(dto),
                certificatePfxBytes,
                certificatePassword,
                updateProcedureName,
                configureUpdateProcedure,
                ResolveInvoiceType<T>(),
                itemCountSelector(dto),
                serviceCountSelector?.Invoke(dto) ?? 0);
        }

        private byte[] GenerateAndSignPdf(
            Func<byte[]> pdfFactory,
            byte[] certificatePfxBytes,
            string certificatePassword,
            string updateProcedureName,
            Action<OracleCommand> configureUpdateProcedure,
            string invoiceType,
            int itemCount,
            int serviceCount = 0)
        {
            _pdfSigner.ValidateCertificate(certificatePfxBytes, certificatePassword);

            var pdfBytes = pdfFactory();
            var (left, top) = CalculateSignaturePosition(invoiceType, itemCount, serviceCount);

            var signedPdfBytes = _pdfSigner.SignPdfWithDigitalCertificate(
                pdfBytes, 
                certificatePfxBytes, 
                certificatePassword,
                options =>
                {
                    options.Left = left;
                    options.Top = top;
                    options.Margin = new Padding(0);
                });

            _pdfSigner.UpdateFinalPDF(
                procedureName: updateProcedureName,
                PDF: signedPdfBytes,
                configureParameters: configureUpdateProcedure);

            return signedPdfBytes;
        }

        private static PdfTemplateContext BuildTemplateContext(IConfiguration configuration, IHostEnvironment hostEnvironment)
        {
            return new PdfTemplateContext
            {
                CompanyName = configuration["CompanyInfo:Name"],
                TaxCode = configuration["CompanyInfo:TaxCode"],
                Address = configuration["CompanyInfo:Address"],
                Phone = configuration["CompanyInfo:Phone"],
                Email = configuration["CompanyInfo:Email"],
                LogoBytes = LoadLogoBytes(configuration["CompanyInfo:LogoPath"], hostEnvironment)
            };
        }

        private static byte[]? LoadLogoBytes(string? relativePath, IHostEnvironment hostEnvironment)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;

            var candidatePaths = new List<string>();
            if (!string.IsNullOrEmpty(hostEnvironment.ContentRootPath))
            {
                candidatePaths.Add(Path.Combine(hostEnvironment.ContentRootPath, relativePath));
            }
            candidatePaths.Add(Path.Combine(AppContext.BaseDirectory, relativePath));
            candidatePaths.Add(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
            candidatePaths.Add(relativePath);

            foreach (var path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    return File.ReadAllBytes(path);
                }
            }

            return null;
        }

        private IPdfTemplate<T> ResolveTemplate<T>()
        {
            if (typeof(T) == typeof(ImportStockDto))
                return (IPdfTemplate<T>)_importTemplate;
            if (typeof(T) == typeof(ExportStockDto))
                return (IPdfTemplate<T>)_exportTemplate;
            if (typeof(T) == typeof(InvoiceDto))
                return (IPdfTemplate<T>)_salesTemplate;

            throw new NotSupportedException($"No PDF template registered for type {typeof(T).Name}.");
        }

        private static string ResolveInvoiceType<T>()
        {
            if (typeof(T) == typeof(ImportStockDto)) return "Import";
            if (typeof(T) == typeof(ExportStockDto)) return "Export";
            if (typeof(T) == typeof(InvoiceDto)) return "Invoice";
            return typeof(T).Name;
        }

        private static (int left, int top) CalculateSignaturePosition(string invoiceType, int itemCount = 0, int serviceCount = 0)
        {
            const double pageWidthPoints = 595;
            const double margin = 25;
            const double signatureWidth = 170;
            const double pointsToPixels = 96.0 / 72.0;

            double leftPoints = pageWidthPoints - margin - signatureWidth - 10;
            int leftPixels = (int)(leftPoints * pointsToPixels);

            double headerHeight = 120;
            double tableHeaderHeight = 30;
            double rowHeight = 20;
            double totalsHeight = 30;
            double signatureTextHeight = 50;

            double contentHeight = headerHeight;

            if (invoiceType == "Import" || invoiceType == "Export")
            {
                contentHeight += tableHeaderHeight + itemCount * rowHeight + totalsHeight;
            }
            else
            {
                contentHeight += tableHeaderHeight;
                contentHeight += itemCount * rowHeight;
                if (itemCount > 0) contentHeight += 20;
                if (serviceCount > 0)
                {
                    contentHeight += tableHeaderHeight;
                    contentHeight += serviceCount * rowHeight;
        }
                contentHeight += totalsHeight + 20;
            }

            contentHeight += signatureTextHeight;
            int topPixels = (int)(contentHeight * pointsToPixels);
            topPixels = Math.Clamp(topPixels, 200, 700);

            return (leftPixels, topPixels);
        }
    }
}
