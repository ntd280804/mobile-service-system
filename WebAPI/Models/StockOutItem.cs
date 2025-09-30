using System;
using System.Collections.Generic;

namespace WebAPI.Models;

public partial class StockOutItem
{
    public decimal StockoutItemId { get; set; }

    public decimal StockoutId { get; set; }

    public decimal PartId { get; set; }

    public virtual Part Part { get; set; } = null!;

    public virtual StockOut Stockout { get; set; } = null!;
}
