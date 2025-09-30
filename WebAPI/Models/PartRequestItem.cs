using System;
using System.Collections.Generic;

namespace WebAPI.Models;

public partial class PartRequestItem
{
    public decimal RequestItemId { get; set; }

    public decimal RequestId { get; set; }

    public decimal PartId { get; set; }

    public virtual Part Part { get; set; } = null!;

    public virtual PartRequest Request { get; set; } = null!;
}
