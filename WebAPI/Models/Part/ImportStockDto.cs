using System;
using System.Collections.Generic;
namespace WebAPI.Models.Part
{
    public class ImportStockDto
    {
        public int StockInId { get; set; }
        public string EmpUsername { get; set; }
        public string Note { get; set; }
        public DateTime InDate { get; set; }
        public string PrivateKey { get; set; }
        public List<ImportItemDto> Items { get; set; }
    }
}
