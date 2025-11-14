using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebAPI.Areas.Admin.Controllers;
using Oracle.ManagedDataAccess.Client;
using MobileServiceSystem.Signing;

namespace WebAPI.Services
{
    public class InvoicePdfService
    {
        private readonly PdfSignatureService? _pdfSigner;

        public InvoicePdfService()
        {
        }

        public InvoicePdfService(PdfSignatureService pdfSigner)
        {
            _pdfSigner = pdfSigner;
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

                    page.Header().Row(row =>
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

			var signedPdfBytes = _pdfSigner.SignPdfWithDigitalCertificate(pdfBytes, certificatePfxBytes, certificatePassword);

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

                    page.Header().Row(row =>
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

            var signedPdfBytes = _pdfSigner.SignPdfWithDigitalCertificate(pdfBytes, certificatePfxBytes, certificatePassword);

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

                    page.Header().Row(row =>
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

            var signedPdfBytes = _pdfSigner.SignPdfWithDigitalCertificate(pdfBytes, certificatePfxBytes, certificatePassword);

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


