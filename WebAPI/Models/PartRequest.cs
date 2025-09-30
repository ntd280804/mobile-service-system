using System;
using System.Collections.Generic;

namespace WebAPI.Models;

public partial class PartRequest
{
    public decimal RequestId { get; set; }

    public decimal OrderId { get; set; }

    public decimal EmpId { get; set; }

    public DateTime RequestDate { get; set; }

    public string Status { get; set; } = null!;

    public virtual Employee Emp { get; set; } = null!;

    public virtual OrderRequest Order { get; set; } = null!;

    public virtual ICollection<PartRequestItem> PartRequestItems { get; set; } = new List<PartRequestItem>();
}
