namespace WebAPI.Models.Export
{
    public class ExportItemDto
    {
        public string PartName { get; set; }
        public string Manufacturer { get; set; }
        public string Serial { get; set; }
        public long Price { get; set; }
    }

    public class ExportStockDto
    {
        public int StockOutId { get; set; }
        public string EmpUsername { get; set; }
        public string Note { get; set; }
        public DateTime OutDate { get; set; }
        public List<ExportItemDto> Items { get; set; }
    }

    public class CreateExportFromOrderDto
    {
        public string EmpUsername { get; set; }
        public int OrderId { get; set; }
        public string CertificatePfxBase64 { get; set; }
        public string CertificatePassword { get; set; }
        public string PrivateKey { get; set; }
    }
}
