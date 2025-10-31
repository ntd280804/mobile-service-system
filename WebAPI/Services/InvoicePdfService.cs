using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebAPI.Areas.Admin.Controllers;

namespace WebAPI.Services
{
    public class InvoicePdfService
    {
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

                        stack.Item().PaddingTop(16).Stack(sign =>
                        {
                            sign.Spacing(6);
                            // --- Chữ ký số đẹp hơn ---
                            sign.Item().AlignRight().Text(text =>
                            {
                                text.Line("Chữ ký số:").SemiBold();
                                text.Line("(Đã ký số)").Italic().FontSize(12);
                                text.Line(dto.EmpUsername).FontSize(11);
                            });
                            // No verify URL printed
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

        private static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? string.Empty;
            return text.Substring(0, Math.Max(0, max - 3)) + "...";
        }
    }
}


