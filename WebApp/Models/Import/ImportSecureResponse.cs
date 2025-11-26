namespace WebApp.Models.Import
{
    public class ImportSecureResponse
    {
        public bool Success { get; set; }
        public string? Type { get; set; } // "pdf" or null
        public string? PdfBase64 { get; set; }
        public string? FileName { get; set; }
        public string? Message { get; set; }
    }
}
