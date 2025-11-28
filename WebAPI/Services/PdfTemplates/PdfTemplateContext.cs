using QuestPDF.Helpers;

namespace WebAPI.Services.PdfTemplates
{
    public class PdfTemplateContext
    {
        public byte[]? LogoBytes { get; init; }
        public string? CompanyName { get; init; }
        public string? TaxCode { get; init; }
        public string? Address { get; init; }
        public string? Phone { get; init; }
        public string? Email { get; init; }
        public string CurrencyCulture { get; init; } = "vi-VN";
        public string SignatureLabel { get; init; } = "Chữ ký số:";
        public string? SignatureNote { get; init; } = "(Đã ký số)";
    }
}

