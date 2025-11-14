namespace WebApp.Models.Part
{
    public class PartDto
    {
        public decimal PartId { get; set; }
        public string Name { get; set; }
        public string Manufacturer { get; set; }
        public string Serial { get; set; }
        public byte[] QRImage { get; set; }
        public string Status { get; set; }
        public decimal StockinID { get; set; }
        public decimal? OrderId { get; set; }
        public decimal? Price { get; set; }
    }
}

