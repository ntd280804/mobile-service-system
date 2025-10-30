using System;
namespace WebAPI.Models.Appointment
{
    public class AppointmentDto
    {
        public int AppointmentId { get; set; }
        public string CustomerPhone { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string Status { get; set; }
        public string Description { get; set; }
    }
}
