namespace WebAPI.Models.Order
{
    public class CreateOrderRequest
    {
        public string CustomerPhone { get; set; } = string.Empty;
        public string ReceiverEmpName { get; set; } = string.Empty;
        public string HandlerEmpName { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<OrderServiceItem> ServiceItems { get; set; } = new List<OrderServiceItem>();
    }

    public class OrderServiceItem
    {
        public int ServiceId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}
