using System;
using System.Collections.Generic;

namespace WebAPI.Models;

public partial class CustomerKey
{
    public string CustomerId { get; set; } = null!;

    public string? PrivateKey { get; set; }

    public virtual Customer Customer { get; set; } = null!;
}
