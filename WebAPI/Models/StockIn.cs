using System;
using System.Collections.Generic;

namespace WebAPI.Models;

public partial class StockIn
{
    public decimal StockinId { get; set; }

    public decimal EmpId { get; set; }

    public DateTime InDate { get; set; }

    public string? Note { get; set; }

    public virtual Employee Emp { get; set; } = null!;

    public virtual ICollection<StockInItem> StockInItems { get; set; } = new List<StockInItem>();
}
