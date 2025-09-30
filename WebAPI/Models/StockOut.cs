using System;
using System.Collections.Generic;

namespace WebAPI.Models;

public partial class StockOut
{
    public decimal StockoutId { get; set; }

    public decimal OrderId { get; set; }

    public decimal EmpId { get; set; }

    public DateTime OutDate { get; set; }

    public string? Note { get; set; }

    public virtual Employee Emp { get; set; } = null!;

    public virtual OrderRequest Order { get; set; } = null!;

    public virtual ICollection<StockOutItem> StockOutItems { get; set; } = new List<StockOutItem>();
}
