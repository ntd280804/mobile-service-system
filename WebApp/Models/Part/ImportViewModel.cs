namespace WebApp.Models.Part
{
    public class ImportViewModel
    {
        public int StockInId { get; set; }
        public string EmpUsername { get; set; }
        public DateTime OutDate { get; set; }
        public string Note { get; set; }
    }
    public class ExportViewModel
    {
        public int StockOutId { get; set; }
        public string EmpUsername { get; set; }
        public DateTime OutDate { get; set; }
        public string Note { get; set; }
    }
}

