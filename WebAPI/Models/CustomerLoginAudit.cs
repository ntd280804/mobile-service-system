using System;
using System.Collections.Generic;

namespace WebAPI.Models;

public partial class CustomerLoginAudit
{
    public decimal AuditId { get; set; }

    public string? Username { get; set; }

    public DateTime? LoginTime { get; set; }

    public string? Status { get; set; }
}
