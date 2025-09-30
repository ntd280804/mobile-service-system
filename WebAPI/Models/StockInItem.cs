using System;
using System.Collections.Generic;

namespace WebAPI.Models;

public partial class StockInItem
{
    public decimal StockinItemId { get; set; }

    public decimal StockinId { get; set; }

    public string PartName { get; set; } = null!;

    public string? Manufacturer { get; set; }

    public string Serial { get; set; } = null!;

    public string? Status { get; set; }

    public virtual ICollection<Part> Parts { get; set; } = new List<Part>();

    public virtual StockIn Stockin { get; set; } = null!;
}
