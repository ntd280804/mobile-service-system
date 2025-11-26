namespace WebApp.Models.Export
{
    public class ExportSecureResponse
    {
        public bool Success { get; set; }
        public string? Type { get; set; } // "pdf" or null
        public string? ExportPdfBase64 { get; set; }
        public string? ExportFileName { get; set; }
        public string? InvoicePdfBase64 { get; set; }
        public string? InvoiceFileName { get; set; }
        public int? InvoiceId { get; set; }
        public int? StockOutId { get; set; }
        public string? Message { get; set; }
    }
}
