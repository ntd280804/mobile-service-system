using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebAPI.Models.Export;

namespace WebAPI.Services.PdfTemplates
{
    public class ExportInvoiceTemplate : IPdfTemplate<ExportStockDto>
    {
        public byte[] GeneratePdf(ExportStockDto dto, PdfTemplateContext context)
        {
            var culture = new CultureInfo(context.CurrencyCulture);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(25);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    BuildHeader(page, dto, context);

                    page.Content().Stack(stack =>
                    {
                        stack.Spacing(12);

                        if (!string.IsNullOrWhiteSpace(dto.Note))
                        {
                            stack.Item().Text(text =>
                            {
                                text.Span("Ghi chú: ").SemiBold();
                                text.Span(dto.Note);
                            });
                        }

                        stack.Item().Element(content => BuildItemsTable(content, dto.Items ?? new List<ExportItemDto>(), culture));
                        stack.Item().Element(content => BuildSignatureBlock(content, dto.EmpUsername, context));
                    });

                    BuildFooter(page);
                });
            });

            return document.GeneratePdf();
        }

        private static void BuildHeader(PageDescriptor page, ExportStockDto dto, PdfTemplateContext context)
        {
            page.Header().Column(column =>
            {
                column.Item().Row(row =>
                {
                    if (context.LogoBytes != null && context.LogoBytes.Length > 0)
                    {
                        row.ConstantItem(60).Image(context.LogoBytes);
                    }

                    row.RelativeItem().PaddingLeft(10).Stack(stack =>
                    {
                        if (!string.IsNullOrEmpty(context.CompanyName))
                            stack.Item().Text(context.CompanyName).SemiBold().FontSize(14);
                        if (!string.IsNullOrEmpty(context.TaxCode))
                            stack.Item().Text($"MST: {context.TaxCode}").FontSize(10);
                        if (!string.IsNullOrEmpty(context.Address))
                            stack.Item().Text(context.Address).FontSize(10);
                        if (!string.IsNullOrEmpty(context.Phone))
                            stack.Item().Text($"ĐT: {context.Phone}").FontSize(10);
                        if (!string.IsNullOrEmpty(context.Email))
                            stack.Item().Text($"Email: {context.Email}").FontSize(10);
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
                });
            });
        }

        private static void BuildItemsTable(IContainer container, IList<ExportItemDto> items, CultureInfo culture)
        {
            container.Table(table =>
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
                    BuildHeaderCell(header.Cell(), "Tên linh kiện");
                    BuildHeaderCell(header.Cell(), "Hãng");
                    BuildHeaderCell(header.Cell(), "Serial");
                    BuildHeaderCell(header.Cell(), "Giá", alignRight: true);
                });

                foreach (var item in items)
                {
                    BuildBodyCell(table.Cell(), item.PartName);
                    BuildBodyCell(table.Cell(), item.Manufacturer);
                    BuildBodyCell(table.Cell(), item.Serial);
                    BuildBodyCell(table.Cell(), string.Format(culture, "{0:C0}", item.Price), alignRight: true);
                }

                var total = items.Sum(x => (decimal)x.Price);
                table.Cell().ColumnSpan(3).AlignRight().PaddingTop(6).Text(t => t.Span("Tổng cộng").SemiBold());
                table.Cell().AlignRight().PaddingTop(6).Text(t => t.Span(string.Format(culture, "{0:C0}", total)).SemiBold());
            });
        }

        private static void BuildSignatureBlock(IContainer container, string? username, PdfTemplateContext context)
        {
            container.PaddingTop(16).Stack(sign =>
            {
                sign.Item().AlignRight().Text(text =>
                {
                    text.Line(context.SignatureLabel).SemiBold();
                    if (!string.IsNullOrEmpty(context.CompanyName))
                    {
                        text.Line(context.CompanyName).FontSize(11);
                    }
                });
            });
        }

        private static void BuildFooter(PageDescriptor page)
        {
            page.Footer().AlignCenter().Text(txt =>
            {
                txt.Span("Generated by HealthCare • ").FontSize(9).FontColor(Colors.Grey.Darken1);
                txt.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        }

        private static void BuildHeaderCell(IContainer container, string text, bool alignRight = false)
        {
            var target = alignRight ? container.AlignRight() : container;
            target.PaddingVertical(4)
                  .BorderBottom(1)
                  .BorderColor(Colors.Grey.Lighten2)
                  .Text(t => t.Span(text).SemiBold());
        }

        private static void BuildBodyCell(IContainer container, string? text, bool alignRight = false)
        {
            var target = alignRight ? container.AlignRight() : container;
            target.PaddingVertical(4)
                  .BorderBottom(1)
                  .BorderColor(Colors.Grey.Lighten4)
                  .Text(text ?? string.Empty);
        }
    }
}