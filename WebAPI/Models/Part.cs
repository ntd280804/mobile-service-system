using System;
using System.Collections.Generic;

namespace WebAPI.Models;

public partial class Part
{
    public decimal PartId { get; set; }

    public string Name { get; set; } = null!;

    public string? Manufacturer { get; set; }

    public string Serial { get; set; } = null!;

    public string Status { get; set; } = null!;

    public decimal StockinItemId { get; set; }

    public decimal? OrderId { get; set; }

    public virtual OrderRequest? Order { get; set; }

    public virtual ICollection<PartRequestItem> PartRequestItems { get; set; } = new List<PartRequestItem>();

    public virtual ICollection<StockOutItem> StockOutItems { get; set; } = new List<StockOutItem>();

    public virtual StockInItem StockinItem { get; set; } = null!;
}
