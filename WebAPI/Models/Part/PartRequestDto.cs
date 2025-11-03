using System;
using System.Collections.Generic;

namespace WebAPI.Models.Part
{
    public class PartRequestDto
    {
        public int REQUEST_ID { get; set; }
        public int ORDER_ID { get; set; }
        public string EmpUsername { get; set; }
        public DateTime REQUEST_DATE { get; set; }
        public string STATUS { get; set; }
        public List<PartRequestItemDto> Items { get; set; }
    }
}
