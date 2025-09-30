using System;
using System.Collections.Generic;

namespace WebAPI.Models;

public partial class CustomerAppointment
{
    public decimal AppointmentId { get; set; }

    public string CustomerPhone { get; set; } = null!;

    public DateTime AppointmentDate { get; set; }

    public string? Status { get; set; }

    public string? Description { get; set; }

    public virtual Customer CustomerPhoneNavigation { get; set; } = null!;
}
