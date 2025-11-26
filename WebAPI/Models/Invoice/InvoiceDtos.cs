using System;
using System.Collections.Generic;

namespace WebAPI.Models.Invoice
{
    public class GenerateInvoicePdfDto
    {
        public int InvoiceId { get; set; }
        public string CertificatePfxBase64 { get; set; } = string.Empty;
        public string CertificatePassword { get; set; } = string.Empty;
    }

    public class InvoiceDto
    {
        public int InvoiceId { get; set; }
        public int StockOutId { get; set; }
        public string CustomerPhone { get; set; } = string.Empty;
        public string EmpUsername { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<InvoiceItemDto> Items { get; set; } = new();
        public List<InvoiceServiceDto> Services { get; set; } = new();
    }

    public class InvoiceItemDto
    {
        public int PartId { get; set; }
        public string PartName { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string Serial { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class InvoiceServiceDto
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}
