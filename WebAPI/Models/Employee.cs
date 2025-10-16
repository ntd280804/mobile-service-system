using System;
using System.Collections.Generic;

namespace WebAPI.Models;

public partial class Employee
{
    public decimal EmpId { get; set; }

    public string FullName { get; set; } = null!;

    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? PublicKey { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string Email { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();

    public virtual EmployeeKey? EmployeeKey { get; set; }

    public virtual ICollection<EmployeeShift> EmployeeShifts { get; set; } = new List<EmployeeShift>();

    public virtual ICollection<OrderRequest> OrderRequests { get; set; } = new List<OrderRequest>();

    public virtual ICollection<PartRequest> PartRequests { get; set; } = new List<PartRequest>();

    public virtual ICollection<StockIn> StockIns { get; set; } = new List<StockIn>();

    public virtual ICollection<StockOut> StockOuts { get; set; } = new List<StockOut>();
}
