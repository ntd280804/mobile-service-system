using System;
using System.Collections.Generic;

namespace WebAPI.Models;

public partial class OrderRequest
{
    public decimal OrderId { get; set; }

    public string CustomerPhone { get; set; } = null!;

    public decimal ReceiverEmp { get; set; }

    public string OrderType { get; set; } = null!;

    public DateTime ReceivedDate { get; set; }

    public string Status { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();

    public virtual Customer CustomerPhoneNavigation { get; set; } = null!;

    public virtual ICollection<PartRequest> PartRequests { get; set; } = new List<PartRequest>();

    public virtual ICollection<Part> Parts { get; set; } = new List<Part>();

    public virtual Employee ReceiverEmpNavigation { get; set; } = null!;

    public virtual ICollection<StockOut> StockOuts { get; set; } = new List<StockOut>();
}
