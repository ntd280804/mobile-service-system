using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebAPI.Models.Invoice;

namespace WebAPI.Services.PdfTemplates
{
    public class SalesInvoiceTemplate : IPdfTemplate<InvoiceDto>
    {
        public byte[] GeneratePdf(InvoiceDto dto, PdfTemplateContext context)
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

                        if (dto.Items?.Any() == true)
                        {
                            stack.Item().Text("Linh kiện:").SemiBold().FontSize(12);
                            stack.Item().Element(content => BuildItemsTable(content, dto.Items, culture));
                        }

                        if (dto.Services?.Any() == true)
                        {
                            stack.Item().PaddingTop(8).Text("Dịch vụ:").SemiBold().FontSize(12);
                            stack.Item().Element(content => BuildServicesTable(content, dto.Services, culture));
                        }

                        stack.Item().PaddingTop(12).Element(content => BuildTotalsTable(content, dto.TotalAmount, culture));
                        stack.Item().PaddingTop(16).Element(content => BuildSignatureBlock(content, dto.EmpUsername, context));
                    });

                    BuildFooter(page);
                });
            });

            return document.GeneratePdf();
        }

        private static void BuildHeader(PageDescriptor page, InvoiceDto dto, PdfTemplateContext context)
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
                        stack.Item().Text("HÓA ĐƠN BÁN HÀNG").SemiBold().FontSize(18);
                        stack.Item().Text($"Mã hóa đơn: #{dto.InvoiceId}");
                        stack.Item().Text($"Ngày: {dto.InvoiceDate:dd/MM/yyyy HH:mm}");
                        stack.Item().Text($"Khách hàng: {dto.CustomerPhone}");
                        stack.Item().Text($"Nhân viên: {dto.EmpUsername}");
                    });
                });
            });
        }

        private static void BuildItemsTable(IContainer container, IList<InvoiceItemDto> items, CultureInfo culture)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
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

                var itemsTotal = items.Sum(x => (decimal)x.Price);
                table.Cell().ColumnSpan(3).AlignRight().PaddingTop(6).Text(t => t.Span("Tổng linh kiện:").SemiBold());
                table.Cell().AlignRight().PaddingTop(6).Text(t => t.Span(string.Format(culture, "{0:C0}", itemsTotal)).SemiBold());
            });
        }

        private static void BuildServicesTable(IContainer container, IList<InvoiceServiceDto> services, CultureInfo culture)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    BuildHeaderCell(header.Cell(), "Tên dịch vụ");
                    BuildHeaderCell(header.Cell(), "SL", alignRight: true);
                    BuildHeaderCell(header.Cell(), "Đơn giá", alignRight: true);
                    BuildHeaderCell(header.Cell(), "Thành tiền", alignRight: true);
                });

                foreach (var service in services)
                {
                    var serviceTotal = service.Price * service.Quantity;
                    BuildBodyCell(table.Cell(), service.ServiceName);
                    BuildBodyCell(table.Cell(), service.Quantity.ToString(), alignRight: true);
                    BuildBodyCell(table.Cell(), string.Format(culture, "{0:C0}", service.Price), alignRight: true);
                    BuildBodyCell(table.Cell(), string.Format(culture, "{0:C0}", serviceTotal), alignRight: true);
                }

                var servicesTotal = services.Sum(x => x.Price * x.Quantity);
                table.Cell().ColumnSpan(3).AlignRight().PaddingTop(6).Text(t => t.Span("Tổng dịch vụ:").SemiBold());
                table.Cell().AlignRight().PaddingTop(6).Text(t => t.Span(string.Format(culture, "{0:C0}", servicesTotal)).SemiBold());
            });
        }

        private static void BuildTotalsTable(IContainer container, decimal totalAmount, CultureInfo culture)
        {
            container.Table(table =>
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
                     .Text(t => t.Span(string.Format(culture, "{0:C0}", totalAmount)).SemiBold().FontSize(14));
            });
        }

        private static void BuildSignatureBlock(IContainer container, string? username, PdfTemplateContext context)
        {
            container.Stack(sign =>
            {
                sign.Item().AlignRight().Text(text =>
                {
                    text.Line(context.SignatureLabel).SemiBold();
                    if (!string.IsNullOrEmpty(context.SignatureNote))
                    {
                        text.Line(context.SignatureNote).Italic().FontSize(12);
                    }
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