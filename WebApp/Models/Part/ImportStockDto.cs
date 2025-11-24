namespace WebApp.Models.Part
{
    public class ImportStockDto
    {
        public string EmpUsername { get; set; }
        public string Note { get; set; }
        public List<ImportItemDto> Items { get; set; } = new();
        public string CertificatePfxBase64 { get; set; }
        public string CertificatePassword { get; set; }
    }
    public class ExportStockDto
    {
        public string EmpUsername { get; set; }
        public decimal orderid { get; set; }
        public string CertificatePfxBase64 { get; set; }
        public string CertificatePassword { get; set; }
    }
}

