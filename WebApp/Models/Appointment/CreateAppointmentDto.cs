namespace WebApp.Models.Appointment
{
    public class CreateAppointmentDto
    {
        public string CustomerPhone { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string Description { get; set; }
    }
}

