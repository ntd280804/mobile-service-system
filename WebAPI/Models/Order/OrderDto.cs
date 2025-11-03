using System;
namespace WebAPI.Models.Order
{
    public class OrderDto
    {
        public decimal OrderId { get; set; }
        public string CustomerPhone { get; set; } = string.Empty;
        public string ReceiverEmpName { get; set; } = string.Empty;
        public string HandlerEmpName { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty;
        public DateTime ReceivedDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
