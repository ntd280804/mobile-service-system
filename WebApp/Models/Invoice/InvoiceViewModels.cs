using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WebApp.Models.Invoice
{
    public class InvoiceSummaryViewModel
    {
        public int InvoiceId { get; set; }
        public int StockOutId { get; set; }
        public string CustomerPhone { get; set; } = string.Empty;
        public string EmpUsername { get; set; } = string.Empty;

        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}")]
        public DateTime InvoiceDate { get; set; }

        [DisplayFormat(DataFormatString = "{0:N0}")]
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class InvoiceDetailViewModel
    {
        public int InvoiceId { get; set; }
        public int StockOutId { get; set; }
        public string CustomerPhone { get; set; } = string.Empty;
        public string EmpUsername { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<InvoiceItemViewModel> Items { get; set; } = new();
        public List<InvoiceServiceViewModel> Services { get; set; } = new();
    }

    public class InvoiceItemViewModel
    {
        public int PartId { get; set; }
        public string PartName { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string Serial { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class InvoiceServiceViewModel
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class VerifyInvoiceResultViewModel
    {
        public int InvoiceId { get; set; }
        public bool IsValid { get; set; }
    }
}

