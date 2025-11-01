using System;
using System.Collections.Generic;

namespace WebApp.Models.Part
{
    public class CreatePartRequestDto
    {
        public decimal OrderId { get; set; }
        public string EmpUsername { get; set; }
        public string Status { get; set; }
        public DateTime RequestDate { get; set; }
        public List<PartRequestItemDto> Items { get; set; } = new List<PartRequestItemDto>();
    }

    public class PartRequestItemDto
    {
        public int PartId { get; set; }
    }
}

