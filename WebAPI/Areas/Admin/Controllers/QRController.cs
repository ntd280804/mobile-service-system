using Microsoft.AspNetCore.Mvc;
using WebAPI.Services;
namespace WebAPI.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("api/[area]/[controller]")]
    [ApiController]
    public class QRController : ControllerBase
    {
        private readonly QrGeneratorSingleton _qrService;

        public QRController(QrGeneratorSingleton qrService)
        {
            _qrService = qrService;
        }

        [HttpGet("{serial}")]
        public IActionResult GetQRCode(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial))
                return BadRequest("Serial is required.");

            try
            {
                var qrBytes = _qrService.GenerateQRImage(serial);
                return File(qrBytes, "image/png");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error generating QR code", detail = ex.Message });
            }
        }
    }
}