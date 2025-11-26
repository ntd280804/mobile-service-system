using System;
using System.Collections.Generic;

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

    public class CreateOrderRequest
    {
        public string CustomerPhone { get; set; } = string.Empty;
        public string ReceiverEmpName { get; set; } = string.Empty;
        public string HandlerEmpName { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<OrderServiceItem> ServiceItems { get; set; } = new();
    }

    public class OrderServiceItem
    {
        public int ServiceId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class CompleteOrderRequest
    {
        public int OrderId { get; set; }
        public string EmpUsername { get; set; }
    }

    public class ServiceDto
    {
        public decimal ServiceId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
    }

    public class OrderServiceDto
    {
        public decimal OrderId { get; set; }
        public decimal ServiceId { get; set; }
        public string ServiceName { get; set; }
        public string ServiceDescription { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public string Status { get; set; }
    }
}
