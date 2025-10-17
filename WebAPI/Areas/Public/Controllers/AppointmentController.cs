using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAPI.Data;
using WebAPI.Models;

namespace WebAPI.Areas.Public.Controllers
{
    [Area("Public")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class AppointmentController : ControllerBase
    {
        private readonly AppDbContext _context;
        public AppointmentController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/Public/Appointment/book
        [HttpPost("book")]
        public async Task<IActionResult> Book([FromBody] BookAppointmentRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.CustomerPhone))
                return BadRequest(new { message = "CustomerPhone và AppointmentDate là bắt buộc" });
            if (request.AppointmentDate == default)
                return BadRequest(new { message = "AppointmentDate không hợp lệ" });

            try
            {
                // Require existing customer (must register account before booking)
                var customer = await _context.Customers.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Phone == request.CustomerPhone);
                if (customer == null)
                {
                    return BadRequest(new { message = "Khách hàng chưa đăng ký. Vui lòng đăng ký tài khoản trước khi đặt lịch." });
                }

                // Optional: if session has logged-in username, enforce it matches the phone being booked
                var sessionUser = HttpContext.Session.GetString("TempOracleUsername"); // WebAPI side
                if (!string.IsNullOrEmpty(sessionUser) && !string.Equals(sessionUser, request.CustomerPhone, StringComparison.OrdinalIgnoreCase))
                {
                    return Unauthorized(new { message = "Tài khoản đang đăng nhập không khớp số điện thoại đặt lịch." });
                }

                // Generate id via MAX+1 (simple demo)
                var maxId = await _context.CustomerAppointments
                    .Select(a => (decimal?)a.AppointmentId)
                    .MaxAsync() ?? 0;
                var newId = maxId + 1;

                var appointment = new CustomerAppointment
                {
                    AppointmentId = newId,
                    CustomerPhone = request.CustomerPhone,
                    AppointmentDate = request.AppointmentDate.Date,
                    Status = "SCHEDULED",
                    Description = request.Description
                };

                _context.CustomerAppointments.Add(appointment);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Đặt lịch thành công", appointmentId = appointment.AppointmentId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi server", detail = ex.Message });
            }
        }

        // GET: api/Public/Appointment/list (optional helper)
        [HttpGet("list")]
        public async Task<IActionResult> List()
        {
            var items = await _context.CustomerAppointments
                .OrderByDescending(x => x.AppointmentDate)
                .ToListAsync();
            return Ok(items);
        }
    }

    public class BookAppointmentRequest
    {
        public string CustomerPhone { get; set; } = string.Empty;
        public DateTime AppointmentDate { get; set; }
        public string? Description { get; set; }
    }
}
