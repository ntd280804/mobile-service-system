using System;
using System.Collections.Generic;

namespace WebAPI.Models;

public partial class Customer
{
    public string Phone { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Username { get; set; }

    public string? PasswordHash { get; set; }

    public string? PublicKey { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public virtual ICollection<CustomerAppointment> CustomerAppointments { get; set; } = new List<CustomerAppointment>();

    public virtual CustomerKey? CustomerKey { get; set; }

    public virtual ICollection<OrderRequest> OrderRequests { get; set; } = new List<OrderRequest>();
}
