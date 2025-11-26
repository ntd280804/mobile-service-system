using System;
using System.Collections.Generic;

namespace WebAPI.Models.Part
{
    public class PartRequestDto
    {
        public int REQUEST_ID { get; set; }
        public int ORDER_ID { get; set; }
        public string EmpUsername { get; set; } = string.Empty;
        public DateTime REQUEST_DATE { get; set; }
        public string STATUS { get; set; } = string.Empty;
        public List<PartRequestItemDto> Items { get; set; } = new();
    }

    public class PartRequestItemDto
    {
        public int PartId { get; set; }
    }

    public class CreatePartRequestDto
    {
        public decimal OrderId { get; set; }
        public string EmpUsername { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime RequestDate { get; set; }
        public List<PartRequestItemDto> Items { get; set; } = new();
    }

    public class ImportItemDto
    {
        public string PartName { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string Serial { get; set; } = string.Empty;
        public long Price { get; set; }
    }
}
