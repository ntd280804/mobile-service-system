using System;
using System.Collections.Generic;

namespace WebAPI.Models;

public partial class EmployeeShift
{
    public decimal ShiftId { get; set; }

    public decimal EmpId { get; set; }

    public DateTime ShiftDate { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string? Status { get; set; }

    public virtual Employee Emp { get; set; } = null!;
}
