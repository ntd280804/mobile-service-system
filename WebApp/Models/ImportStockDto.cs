namespace WebApp.Models
{
    public class ImportStockDto
    {
        public string EmpUsername { get; set; }
        public string Note { get; set; }
        public string PrivateKey { get; set; }
        public List<ImportItemDto> Items { get; set; } = new();
    }
}


