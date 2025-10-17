using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;

namespace WebAPI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class AppointmentController : ControllerBase
    {
        private readonly AppDbContext _context;
        public AppointmentController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Admin/Appointment/list
        [HttpGet("list")]
        public async Task<IActionResult> List()
        {
            var items = await _context.CustomerAppointments
                .OrderByDescending(x => x.AppointmentDate)
                .Select(x => new
                {
                    appointmentId = x.AppointmentId,
                    customerPhone = x.CustomerPhone,
                    appointmentDate = x.AppointmentDate,
                    status = x.Status,
                    description = x.Description
                })
                .ToListAsync();
            return Ok(items);
        }

        // PUT: api/Admin/Appointment/{id}/status
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(decimal id, [FromBody] UpdateStatusRequest body)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.Status))
                return BadRequest(new { message = "Status is required" });

            var apt = await _context.CustomerAppointments.FirstOrDefaultAsync(a => a.AppointmentId == id);
            if (apt == null) return NotFound(new { message = "Appointment not found" });

            apt.Status = body.Status; // e.g. SCHEDULED, COMPLETED, NO_SHOW
            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật trạng thái thành công" });
        }
    }

    public class UpdateStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }
}
