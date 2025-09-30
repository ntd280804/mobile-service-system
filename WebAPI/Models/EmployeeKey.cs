using System;
using System.Collections.Generic;

namespace WebAPI.Models;

public partial class EmployeeKey
{
    public decimal EmpId { get; set; }

    public string? PrivateKey { get; set; }

    public virtual Employee Emp { get; set; } = null!;
}
