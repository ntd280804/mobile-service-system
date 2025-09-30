using System;
using System.Collections.Generic;

namespace WebAPI.Models;

public partial class Assignment
{
    public decimal AssignId { get; set; }

    public decimal OrderId { get; set; }

    public decimal EmpId { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? Status { get; set; }

    public virtual Employee Emp { get; set; } = null!;

    public virtual OrderRequest Order { get; set; } = null!;
}
