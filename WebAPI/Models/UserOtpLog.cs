using System;
using System.Collections.Generic;

namespace WebAPI.Models;

public partial class UserOtpLog
{
    public decimal Id { get; set; }

    public decimal UserId { get; set; }

    public string Username { get; set; } = null!;

    public string Otp { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public DateTime? ExpiredAt { get; set; }

    public string? Used { get; set; }
}
