using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebAPI.Areas.Admin.Controllers;
using Oracle.ManagedDataAccess.Client;
using MobileServiceSystem.Signing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using GroupDocs.Signature.Domain;

namespace WebAPI.Services
{
    public class InvoicePdfService
    {
        private readonly PdfSignatureService? _pdfSigner;
        private readonly IConfiguration? _configuration;
        private readonly IHostEnvironment? _hostEnvironment;
        private byte[]? _logoBytes;
        private string? _companyName;
        private string? _taxCode;
        private string? _address;
        private string? _phone;
        private string? _email;

        public InvoicePdfService()
        {
        }

        public InvoicePdfService(PdfSignatureService pdfSigner)
        {
            _pdfSigner = pdfSigner;
        }

        public InvoicePdfService(PdfSignatureService pdfSigner, IConfiguration configuration)
        {
            _pdfSigner = pdfSigner;
            _configuration = configuration;
            LoadCompanyInfo();
        }

        public InvoicePdfService(PdfSignatureService pdfSigner, IConfiguration configuration, IHostEnvironment hostEnvironment)
        {
            _pdfSigner = pdfSigner;
            _configuration = configuration;
            _hostEnvironment = hostEnvironment;
            LoadCompanyInfo();
        }

        private void LoadCompanyInfo()
        {
            if (_configuration == null) return;

            _companyName = _configuration["CompanyInfo:Name"];
            _taxCode = _configuration["CompanyInfo:TaxCode"];
            _address = _configuration["CompanyInfo:Address"];
            _phone = _configuration["CompanyInfo:Phone"];
            _email = _configuration["CompanyInfo:Email"];

            var logoPath = _configuration["CompanyInfo:LogoPath"];
            if (!string.IsNullOrEmpty(logoPath))
            {
                try
                {
                    // Try multiple paths
                    var paths = new List<string>();
                    
                    // Add ContentRootPath if available
                    if (_hostEnvironment != null && !string.IsNullOrEmpty(_hostEnvironment.ContentRootPath))
                    {
                        paths.Add(Path.Combine(_hostEnvironment.ContentRootPath, logoPath));
                    }
                    
                    // Add other common paths
                    paths.Add(Path.Combine(AppContext.BaseDirectory, logoPath));
                    paths.Add(Path.Combine(Directory.GetCurrentDirectory(), logoPath));
                    paths.Add(logoPath); // Absolute path

                    foreach (var fullPath in paths)
                    {
                        if (File.Exists(fullPath))
                        {
                            _logoBytes = File.ReadAllBytes(fullPath);
                            break;
                        }
                    }
                }
                catch
                {
                    // Ignore if logo file not found
                }
            }
        }

        // Calculate signature position based on PDF layout
        // A4 page: 595 x 842 points (1 point = 1/72 inch)
        // GroupDocs uses pixels (96 DPI), so 1 point = 96/72 = 1.333 pixels
        // Returns (left, top) in pixels for GroupDocs
        private (int left, int top) CalculateSignaturePosition(string invoiceType, int itemCount = 0, int serviceCount = 0)
        {
            const double pageWidthPoints = 595; // A4 width in points
            const double pageHeightPoints = 842; // A4 height in points
            const double margin = 25;
            const double signatureWidth = 170; // Signature box width in points
            const double signatureHeight = 90; // Signature box height in points
            const double pointsToPixels = 96.0 / 72.0; // Convert points to pixels (96 DPI)

            // Calculate left position: right side of page
            double leftPoints = pageWidthPoints - margin - signatureWidth - 10; // Right aligned with padding
            int leftPixels = (int)(leftPoints * pointsToPixels);

            // Calculate top position: below "Chữ ký số:" text
            // Header: ~120 points (with logo and company info)
            // Content: varies by item/service count
            double headerHeight = 120;
            double tableHeaderHeight = 30;
            double rowHeight = 20;
            double totalsHeight = 30;
            double signatureTextHeight = 50; // "Chữ ký số:" text area
            double footerHeight = 20;

            double contentHeight = headerHeight;
            
            if (invoiceType == "Import" || invoiceType == "Export")
            {
                contentHeight += tableHeaderHeight;
                contentHeight += itemCount * rowHeight;
                contentHeight += totalsHeight;
            }
            else if (invoiceType == "Invoice")
            {
                contentHeight += tableHeaderHeight;
                contentHeight += itemCount * rowHeight;
                if (itemCount > 0) contentHeight += 20; // Spacing
                if (serviceCount > 0)
                {
                    contentHeight += tableHeaderHeight;
                    contentHeight += serviceCount * rowHeight;
                }
                contentHeight += totalsHeight + 20; // Total row
            }

            contentHeight += signatureTextHeight;
            
            // Top position: from top of page
            double topPoints = contentHeight;
            int topPixels = (int)(topPoints * pointsToPixels);

            // Ensure reasonable position
            if (topPixels < 200) topPixels = 200;
            if (topPixels > 700) topPixels = 700;

            return (leftPixels, topPixels);
        }

        public byte[] GenerateImportInvoicePdf(ImportStockDto dto, string signature, byte[]? qrPngBytes, string? verifyUrl)
        {
            var currencyCulture = new CultureInfo("vi-VN");

			var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(25);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Column(column =>
                    {
                        // Company info with logo
                        column.Item().Row(row =>
                        {
                            if (_logoBytes != null && _logoBytes.Length > 0)
                            {
                                row.ConstantItem(60).Image(_logoBytes);
                            }
                            row.RelativeItem().PaddingLeft(10).Stack(stack =>
                            {
                                if (!string.IsNullOrEmpty(_companyName))
                                    stack.Item().Text(_companyName).SemiBold().FontSize(14);
                                if (!string.IsNullOrEmpty(_taxCode))
                                    stack.Item().Text($"MST: {_taxCode}").FontSize(10);
                                if (!string.IsNullOrEmpty(_address))
                                    stack.Item().Text(_address).FontSize(10);
                                if (!string.IsNullOrEmpty(_phone))
                                    stack.Item().Text($"ĐT: {_phone}").FontSize(10);
                                if (!string.IsNullOrEmpty(_email))
                                    stack.Item().Text($"Email: {_email}").FontSize(10);
                            });
                        });

                        column.Item().PaddingTop(10).Row(row =>
                    {
                        row.AutoItem().Stack(stack =>
                        {
                            stack.Spacing(2);
                            stack.Item().Text("HÓA ĐƠN NHẬP KHO").SemiBold().FontSize(18);
                            stack.Item().Text($"Mã phiếu: #{dto.StockInId}");
                            stack.Item().Text($"Ngày: {dto.InDate:dd/MM/yyyy HH:mm}");
                            stack.Item().Text($"Nhân viên: {dto.EmpUsername}");
                        });

                        if (qrPngBytes != null && qrPngBytes.Length > 0)
                        {
                            row.RelativeItem().AlignRight().Image(qrPngBytes);
                        }
                        });
                    });

                    page.Content().Stack(stack =>
                    {
                        stack.Spacing(12);

                        if (!string.IsNullOrWhiteSpace(dto.Note))
                        {
                            stack.Item().Text($"Ghi chú: {dto.Note}").Italic();
                        }

                        stack.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(4);   // Tên linh kiện
                                columns.RelativeColumn(3);   // Hãng
                                columns.RelativeColumn(3);   // Serial
                                columns.RelativeColumn(2);   // Giá
                            });

                            table.Header(header =>
                            {
                                header.Cell()
                                      .PaddingVertical(4)
                                      .BorderBottom(1)
                                      .BorderColor(Colors.Grey.Lighten2)
                                      .Text(t => t.Span("Tên linh kiện").SemiBold());

                                header.Cell()
                                      .PaddingVertical(4)
                                      .BorderBottom(1)
                                      .BorderColor(Colors.Grey.Lighten2)
                                      .Text(t => t.Span("Hãng").SemiBold());

                                header.Cell()
                                      .PaddingVertical(4)
                                      .BorderBottom(1)
                                      .BorderColor(Colors.Grey.Lighten2)
                                      .Text(t => t.Span("Serial").SemiBold());

                                header.Cell()
                                      .PaddingVertical(4)
                                      .BorderBottom(1)
                                      .BorderColor(Colors.Grey.Lighten2)
                                      .AlignRight()
                                      .Text(t => t.Span("Giá").SemiBold());
                            });

                            foreach (var item in dto.Items ?? new List<ImportItemDto>())
                            {
                                table.Cell()
                                     .PaddingVertical(4)
                                     .BorderBottom(1)
                                     .BorderColor(Colors.Grey.Lighten4)
                                     .Text(item.PartName);

                                table.Cell()
                                     .PaddingVertical(4)
                                     .BorderBottom(1)
                                     .BorderColor(Colors.Grey.Lighten4)
                                     .Text(item.Manufacturer);

                                table.Cell()
                                     .PaddingVertical(4)
                                     .BorderBottom(1)
                                     .BorderColor(Colors.Grey.Lighten4)
                                     .Text(item.Serial);

                                table.Cell()
                                     .PaddingVertical(4)
                                     .BorderBottom(1)
                                     .BorderColor(Colors.Grey.Lighten4)
                                     .AlignRight()
                                     .Text(string.Format(currencyCulture, "{0:C0}", item.Price));
                            }

                            var total = (dto.Items ?? new List<ImportItemDto>()).Sum(x => (decimal)x.Price);

                            table.Cell().ColumnSpan(3).AlignRight().PaddingTop(6).Text(t => t.Span("Tổng cộng").SemiBold());
                            table.Cell().AlignRight().PaddingTop(6).Text(t => t.Span(string.Format(currencyCulture, "{0:C0}", total)).SemiBold());
                        });

                        // Khu vực chữ ký số (không hiển thị tên Admin hay dòng "(Đã ký số)")
                        stack.Item().PaddingTop(16).Stack(sign =>
                        {
                            sign.Spacing(6);
                            sign.Item().AlignRight().Text(text =>
                            {
                                text.Line("Chữ ký số:").SemiBold();
                                text.Line("(Đã ký số)").Italic().FontSize(12);
                                text.Line(dto.EmpUsername).FontSize(11);
                            });
                        });
                    });

                    page.Footer().AlignCenter().Text(txt =>
                    {
                        txt.Span("Generated by Mobile Service System • ").FontSize(9).FontColor(Colors.Grey.Darken1);
                        txt.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                });
            });

            return document.GeneratePdf();
        }

        
		/// <summary>
		/// Generates Import PDF, signs the entire PDF using a PFX certificate (GroupDocs), then saves the signed PDF bytes via the supplied update procedure.
		/// Returns the signed PDF bytes.
		/// </summary>
		public byte[] GenerateImportInvoicePdfAndSignWithCertificate(
			ImportStockDto dto,
			byte[] certificatePfxBytes,
			string certificatePassword,
			Action<OracleCommand> configureUpdateProcedure,
			byte[]? qrPngBytes,
			string? verifyUrl)
		{
			var pdfBytes = GenerateImportInvoicePdf(dto, signature: string.Empty, qrPngBytes: qrPngBytes, verifyUrl: verifyUrl);

			if (_pdfSigner == null) throw new InvalidOperationException("PdfSignatureService is not configured.");

			// Calculate signature position below "Chữ ký số:" text
			var (left, top) = CalculateSignaturePosition("Import", dto.Items?.Count ?? 0);

			var signedPdfBytes = _pdfSigner.SignPdfWithDigitalCertificate(
				pdfBytes, 
				certificatePfxBytes, 
				certificatePassword,
				options => {
					// Set absolute position - when Left and Top are set, alignment is ignored
					options.Left = left;
					options.Top = top;
					options.Margin = new Padding(0);
				}
			);

			// Persist the signed PDF as BLOB (parameter defaults to p_signature unless caller binds differently).
			_pdfSigner.UpdateFinalSignature(
				procedureName: "APP.UPDATE_STOCKIN_PDF",
				finalSignature: signedPdfBytes,
				configureParameters: configureUpdateProcedure
			);

			return signedPdfBytes;
		}

        public byte[] GenerateExportInvoicePdf(ExportStockDto dto, string signature, byte[]? qrPngBytes, string? verifyUrl)
        {
            var currencyCulture = new CultureInfo("vi-VN");

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(25);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Column(column =>
                    {
                        // Company info with logo
                        column.Item().Row(row =>
                        {
                            if (_logoBytes != null && _logoBytes.Length > 0)
                            {
                                row.ConstantItem(60).Image(_logoBytes);
                            }
                            row.RelativeItem().PaddingLeft(10).Stack(stack =>
                            {
                                if (!string.IsNullOrEmpty(_companyName))
                                    stack.Item().Text(_companyName).SemiBold().FontSize(14);
                                if (!string.IsNullOrEmpty(_taxCode))
                                    stack.Item().Text($"MST: {_taxCode}").FontSize(10);
                                if (!string.IsNullOrEmpty(_address))
                                    stack.Item().Text(_address).FontSize(10);
                                if (!string.IsNullOrEmpty(_phone))
                                    stack.Item().Text($"ĐT: {_phone}").FontSize(10);
                                if (!string.IsNullOrEmpty(_email))
                                    stack.Item().Text($"Email: {_email}").FontSize(10);
                            });
                        });

                        column.Item().PaddingTop(10).Row(row =>
                    {
                        row.AutoItem().Stack(stack =>
                        {
                            stack.Spacing(2);
                            stack.Item().Text("HÓA ĐƠN XUẤT KHO").SemiBold().FontSize(18);
                            stack.Item().Text($"Mã phiếu: #{dto.StockOutId}");
                            stack.Item().Text($"Ngày: {dto.OutDate:dd/MM/yyyy HH:mm}");
                            stack.Item().Text($"Nhân viên: {dto.EmpUsername}");
                        });

                        if (qrPngBytes != null && qrPngBytes.Length > 0)
                        {
                            row.RelativeItem().AlignRight().Image(qrPngBytes);
                        }
                        });
                    });

                    page.Content().Stack(stack =>
                    {
                        stack.Spacing(12);

                        if (!string.IsNullOrWhiteSpace(dto.Note))
                        {
                            stack.Item().Text(text =>
                            {
                                text.Span("Ghi chú: ").SemiBold();
                                text.Span(Shorten(dto.Note ?? string.Empty, 300));
                            });
                        }

                        stack.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                            });

                            table.Header(header =>
                            {
                                header.Cell()
                                      .PaddingVertical(4)
                                      .BorderBottom(1)
                                      .BorderColor(Colors.Grey.Lighten2)
                                      .Text(t => t.Span("Tên linh kiện").SemiBold());

                                header.Cell()
                                      .PaddingVertical(4)
                                      .BorderBottom(1)
                                      .BorderColor(Colors.Grey.Lighten2)
                                      .Text(t => t.Span("Hãng").SemiBold());

                                header.Cell()
                                      .PaddingVertical(4)
                                      .BorderBottom(1)
                                      .BorderColor(Colors.Grey.Lighten2)
                                      .Text(t => t.Span("Serial").SemiBold());

                                header.Cell()
                                      .PaddingVertical(4)
                                      .BorderBottom(1)
                                      .BorderColor(Colors.Grey.Lighten2)
                                      .AlignRight()
                                      .Text(t => t.Span("Giá").SemiBold());
                            });

                            foreach (var item in dto.Items ?? new List<ExportItemDto>())
                            {
                                table.Cell()
                                     .PaddingVertical(4)
                                     .BorderBottom(1)
                                     .BorderColor(Colors.Grey.Lighten4)
                                     .Text(item.PartName);

                                table.Cell()
                                     .PaddingVertical(4)
                                     .BorderBottom(1)
                                     .BorderColor(Colors.Grey.Lighten4)
                                     .Text(item.Manufacturer);

                                table.Cell()
                                     .PaddingVertical(4)
                                     .BorderBottom(1)
                                     .BorderColor(Colors.Grey.Lighten4)
                                     .Text(item.Serial);

                                table.Cell()
                                     .PaddingVertical(4)
                                     .BorderBottom(1)
                                     .BorderColor(Colors.Grey.Lighten4)
                                     .AlignRight()
                                     .Text(string.Format(currencyCulture, "{0:C0}", item.Price));
                            }

                            var total = (dto.Items ?? new List<ExportItemDto>()).Sum(x => (decimal)x.Price);

                            table.Cell().ColumnSpan(3).AlignRight().PaddingTop(6).Text(t => t.Span("Tổng cộng").SemiBold());
                            table.Cell().AlignRight().PaddingTop(6).Text(t => t.Span(string.Format(currencyCulture, "{0:C0}", total)).SemiBold());
                        });

						stack.Item().PaddingTop(16).AlignRight().Text(text =>
						{
							text.Line("Chữ ký số:").SemiBold();
						});
                    });

                    page.Footer().AlignCenter().Text(txt =>
                    {
                        txt.Span("Generated by Mobile Service System • ").FontSize(9).FontColor(Colors.Grey.Darken1);
                        txt.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                });
            });

            return document.GeneratePdf();
        }

        /// <summary>
        /// Generates Export PDF, signs the entire PDF using a PFX certificate (GroupDocs), then saves the signed PDF bytes via the supplied update procedure.
        /// Returns the signed PDF bytes.
        /// </summary>
        public byte[] GenerateExportInvoicePdfAndSignWithCertificate(
            ExportStockDto dto,
            byte[] certificatePfxBytes,
            string certificatePassword,
            Action<OracleCommand> configureUpdateProcedure,
            byte[]? qrPngBytes,
            string? verifyUrl)
        {
            var pdfBytes = GenerateExportInvoicePdf(dto, signature: string.Empty, qrPngBytes: qrPngBytes, verifyUrl: verifyUrl);

            if (_pdfSigner == null) throw new InvalidOperationException("PdfSignatureService is not configured.");

            // Calculate signature position below "Chữ ký số:" text
            var (left, top) = CalculateSignaturePosition("Export", dto.Items?.Count ?? 0);

            var signedPdfBytes = _pdfSigner.SignPdfWithDigitalCertificate(
                pdfBytes, 
                certificatePfxBytes, 
                certificatePassword,
                options => {
                    // Set absolute position - when Left and Top are set, alignment is ignored
                    options.Left = left;
                    options.Top = top;
                    options.Margin = new Padding(0);
                }
            );

            _pdfSigner.UpdateFinalSignature(
                procedureName: "APP.UPDATE_STOCKOUT_PDF",
                finalSignature: signedPdfBytes,
                configureParameters: configureUpdateProcedure
            );

            return signedPdfBytes;
        }

        public byte[] GenerateInvoicePdf(InvoiceDto dto, string signature, byte[]? qrPngBytes, string? verifyUrl)
        {
            var currencyCulture = new CultureInfo("vi-VN");

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(25);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Column(column =>
                    {
                        // Company info with logo
                        column.Item().Row(row =>
                        {
                            if (_logoBytes != null && _logoBytes.Length > 0)
                            {
                                row.ConstantItem(60).Image(_logoBytes);
                            }
                            row.RelativeItem().PaddingLeft(10).Stack(stack =>
                            {
                                if (!string.IsNullOrEmpty(_companyName))
                                    stack.Item().Text(_companyName).SemiBold().FontSize(14);
                                if (!string.IsNullOrEmpty(_taxCode))
                                    stack.Item().Text($"MST: {_taxCode}").FontSize(10);
                                if (!string.IsNullOrEmpty(_address))
                                    stack.Item().Text(_address).FontSize(10);
                                if (!string.IsNullOrEmpty(_phone))
                                    stack.Item().Text($"ĐT: {_phone}").FontSize(10);
                                if (!string.IsNullOrEmpty(_email))
                                    stack.Item().Text($"Email: {_email}").FontSize(10);
                            });
                        });

                        column.Item().PaddingTop(10).Row(row =>
                    {
                        row.AutoItem().Stack(stack =>
                        {
                            stack.Spacing(2);
                            stack.Item().Text("HÓA ĐƠN BÁN HÀNG").SemiBold().FontSize(18);
                            stack.Item().Text($"Mã hóa đơn: #{dto.InvoiceId}");
                            stack.Item().Text($"Ngày: {dto.InvoiceDate:dd/MM/yyyy HH:mm}");
                            stack.Item().Text($"Khách hàng: {dto.CustomerPhone}");
                            stack.Item().Text($"Nhân viên: {dto.EmpUsername}");
                        });

                        if (qrPngBytes != null && qrPngBytes.Length > 0)
                        {
                            row.RelativeItem().AlignRight().Image(qrPngBytes);
                        }
                        });
                    });

                    page.Content().Stack(stack =>
                    {
                        stack.Spacing(12);

                        // Phần linh kiện (Items)
                        if (dto.Items != null && dto.Items.Any())
                        {
                            stack.Item().Text("Linh kiện:").SemiBold().FontSize(12);
                            stack.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);   // Tên linh kiện
                                    columns.RelativeColumn(2);   // Hãng
                                    columns.RelativeColumn(2);   // Serial
                                    columns.RelativeColumn(2);   // Giá
                                });

                                table.Header(header =>
                                {
                                    header.Cell()
                                          .PaddingVertical(4)
                                          .BorderBottom(1)
                                          .BorderColor(Colors.Grey.Lighten2)
                                          .Text(t => t.Span("Tên linh kiện").SemiBold());

                                    header.Cell()
                                          .PaddingVertical(4)
                                          .BorderBottom(1)
                                          .BorderColor(Colors.Grey.Lighten2)
                                          .Text(t => t.Span("Hãng").SemiBold());

                                    header.Cell()
                                          .PaddingVertical(4)
                                          .BorderBottom(1)
                                          .BorderColor(Colors.Grey.Lighten2)
                                          .Text(t => t.Span("Serial").SemiBold());

                                    header.Cell()
                                          .PaddingVertical(4)
                                          .BorderBottom(1)
                                          .BorderColor(Colors.Grey.Lighten2)
                                          .AlignRight()
                                          .Text(t => t.Span("Giá").SemiBold());
                                });

                                foreach (var item in dto.Items)
                                {
                                    table.Cell()
                                         .PaddingVertical(4)
                                         .BorderBottom(1)
                                         .BorderColor(Colors.Grey.Lighten4)
                                         .Text(item.PartName ?? "");

                                    table.Cell()
                                         .PaddingVertical(4)
                                         .BorderBottom(1)
                                         .BorderColor(Colors.Grey.Lighten4)
                                         .Text(item.Manufacturer ?? "");

                                    table.Cell()
                                         .PaddingVertical(4)
                                         .BorderBottom(1)
                                         .BorderColor(Colors.Grey.Lighten4)
                                         .Text(item.Serial ?? "");

                                    table.Cell()
                                         .PaddingVertical(4)
                                         .BorderBottom(1)
                                         .BorderColor(Colors.Grey.Lighten4)
                                         .AlignRight()
                                         .Text(string.Format(currencyCulture, "{0:C0}", item.Price));
                                }

                                var itemsTotal = dto.Items.Sum(x => (decimal)x.Price);
                                table.Cell().ColumnSpan(3).AlignRight().PaddingTop(6).Text(t => t.Span("Tổng linh kiện:").SemiBold());
                                table.Cell().AlignRight().PaddingTop(6).Text(t => t.Span(string.Format(currencyCulture, "{0:C0}", itemsTotal)).SemiBold());
                            });
                        }

                        // Phần dịch vụ (Services)
                        if (dto.Services != null && dto.Services.Any())
                        {
                            stack.Item().PaddingTop(8).Text("Dịch vụ:").SemiBold().FontSize(12);
                            stack.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(4);   // Tên dịch vụ
                                    columns.RelativeColumn(2);   // Số lượng
                                    columns.RelativeColumn(3);   // Đơn giá
                                    columns.RelativeColumn(2);   // Thành tiền
                                });

                                table.Header(header =>
                                {
                                    header.Cell()
                                          .PaddingVertical(4)
                                          .BorderBottom(1)
                                          .BorderColor(Colors.Grey.Lighten2)
                                          .Text(t => t.Span("Tên dịch vụ").SemiBold());

                                    header.Cell()
                                          .PaddingVertical(4)
                                          .BorderBottom(1)
                                          .BorderColor(Colors.Grey.Lighten2)
                                          .AlignRight()
                                          .Text(t => t.Span("SL").SemiBold());

                                    header.Cell()
                                          .PaddingVertical(4)
                                          .BorderBottom(1)
                                          .BorderColor(Colors.Grey.Lighten2)
                                          .AlignRight()
                                          .Text(t => t.Span("Đơn giá").SemiBold());

                                    header.Cell()
                                          .PaddingVertical(4)
                                          .BorderBottom(1)
                                          .BorderColor(Colors.Grey.Lighten2)
                                          .AlignRight()
                                          .Text(t => t.Span("Thành tiền").SemiBold());
                                });

                                foreach (var service in dto.Services)
                                {
                                    var serviceTotal = (decimal)service.Price * (decimal)service.Quantity;
                                    table.Cell()
                                         .PaddingVertical(4)
                                         .BorderBottom(1)
                                         .BorderColor(Colors.Grey.Lighten4)
                                         .Text(service.ServiceName ?? "");

                                    table.Cell()
                                         .PaddingVertical(4)
                                         .BorderBottom(1)
                                         .BorderColor(Colors.Grey.Lighten4)
                                         .AlignRight()
                                         .Text(service.Quantity.ToString());

                                    table.Cell()
                                         .PaddingVertical(4)
                                         .BorderBottom(1)
                                         .BorderColor(Colors.Grey.Lighten4)
                                         .AlignRight()
                                         .Text(string.Format(currencyCulture, "{0:C0}", service.Price));

                                    table.Cell()
                                         .PaddingVertical(4)
                                         .BorderBottom(1)
                                         .BorderColor(Colors.Grey.Lighten4)
                                         .AlignRight()
                                         .Text(string.Format(currencyCulture, "{0:C0}", serviceTotal));
                                }

                                var servicesTotal = dto.Services.Sum(x => (decimal)x.Price * (decimal)x.Quantity);
                                table.Cell().ColumnSpan(3).AlignRight().PaddingTop(6).Text(t => t.Span("Tổng dịch vụ:").SemiBold());
                                table.Cell().AlignRight().PaddingTop(6).Text(t => t.Span(string.Format(currencyCulture, "{0:C0}", servicesTotal)).SemiBold());
                            });
                        }

                        // Tổng cộng
                        stack.Item().PaddingTop(12).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(9);
                                columns.RelativeColumn(2);
                            });

                            table.Cell()
                                 .PaddingVertical(8)
                                 .BorderTop(2)
                                 .BorderColor(Colors.Grey.Darken1)
                                 .AlignRight()
                                 .Text(t => t.Span("TỔNG CỘNG:").SemiBold().FontSize(14));

                            table.Cell()
                                 .PaddingVertical(8)
                                 .BorderTop(2)
                                 .BorderColor(Colors.Grey.Darken1)
                                 .AlignRight()
                                 .Text(t => t.Span(string.Format(currencyCulture, "{0:C0}", dto.TotalAmount)).SemiBold().FontSize(14));
                        });

                        // Khu vực chữ ký số
                        stack.Item().PaddingTop(16).Stack(sign =>
                        {
                            sign.Spacing(6);
                            sign.Item().AlignRight().Text(text =>
                            {
                                text.Line("Chữ ký số:").SemiBold();
                                text.Line("(Đã ký số)").Italic().FontSize(12);
                                text.Line(dto.EmpUsername).FontSize(11);
                            });
                        });
                    });

                    page.Footer().AlignCenter().Text(txt =>
                    {
                        txt.Span("Generated by Mobile Service System • ").FontSize(9).FontColor(Colors.Grey.Darken1);
                        txt.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                });
            });

            return document.GeneratePdf();
        }

        /// <summary>
        /// Generates Invoice PDF, signs the entire PDF using a PFX certificate (GroupDocs), then saves the signed PDF bytes via the supplied update procedure.
        /// Returns the signed PDF bytes.
        /// </summary>
        public byte[] GenerateInvoicePdfAndSignWithCertificate(
            InvoiceDto dto,
            byte[] certificatePfxBytes,
            string certificatePassword,
            Action<OracleCommand> configureUpdateProcedure,
            byte[]? qrPngBytes,
            string? verifyUrl)
        {
            var pdfBytes = GenerateInvoicePdf(dto, signature: string.Empty, qrPngBytes: qrPngBytes, verifyUrl: verifyUrl);

            if (_pdfSigner == null) throw new InvalidOperationException("PdfSignatureService is not configured.");

            // Calculate signature position below "Chữ ký số:" text
            var (left, top) = CalculateSignaturePosition("Invoice", dto.Items?.Count ?? 0, dto.Services?.Count ?? 0);

            var signedPdfBytes = _pdfSigner.SignPdfWithDigitalCertificate(
                pdfBytes, 
                certificatePfxBytes, 
                certificatePassword,
                options => {
                    // Set absolute position - when Left and Top are set, alignment is ignored
                    options.Left = left;
                    options.Top = top;
                    options.Margin = new Padding(0);
                }
            );

            _pdfSigner.UpdateFinalSignature(
                procedureName: "APP.UPDATE_INVOICE_PDF",
                finalSignature: signedPdfBytes,
                configureParameters: configureUpdateProcedure
            );

            return signedPdfBytes;
        }

        private static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? string.Empty;
            return text.Substring(0, Math.Max(0, max - 3)) + "...";
        }
    }

    // DTOs for Invoice
    public class InvoiceDto
    {
        public int InvoiceId { get; set; }
        public int StockOutId { get; set; }
        public string CustomerPhone { get; set; }
        public string EmpUsername { get; set; }
        public DateTime InvoiceDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; }
        public List<InvoiceItemDto> Items { get; set; }
        public List<InvoiceServiceDto> Services { get; set; }
    }

    public class InvoiceItemDto
    {
        public int PartId { get; set; }
        public string PartName { get; set; }
        public string Manufacturer { get; set; }
        public string Serial { get; set; }
        public decimal Price { get; set; }
    }

    public class InvoiceServiceDto
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}


