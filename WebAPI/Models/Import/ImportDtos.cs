namespace WebAPI.Models.Import
{
    public class ImportItemDto
    {
        public string PartName { get; set; }
        public string Manufacturer { get; set; }
        public string Serial { get; set; }
        public long Price { get; set; }
    }

    public class ImportStockDto
    {
        public int StockInId { get; set; }
        public string EmpUsername { get; set; }
        public string Note { get; set; }
        public DateTime InDate { get; set; }
        public List<ImportItemDto> Items { get; set; }
        public string CertificatePfxBase64 { get; set; }
        public string CertificatePassword { get; set; }
        public string PrivateKey { get; set; }
    }
}
