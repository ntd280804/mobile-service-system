using System;

namespace WebAPI.Models.Appointment
{
    public class AppointmentDto
    {
        public int AppointmentId { get; set; }
        public string CustomerPhone { get; set; } = string.Empty;
        public DateTime AppointmentDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class CreateAppointmentDto
    {
        public string CustomerPhone { get; set; } = string.Empty;
        public DateTime AppointmentDate { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
