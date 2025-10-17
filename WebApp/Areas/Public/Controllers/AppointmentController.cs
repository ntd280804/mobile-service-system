using Microsoft.AspNetCore.Mvc;

namespace WebApp.Areas.Public.Controllers
{
    [Area("Public")]
    public class AppointmentController : Controller
    {
        [HttpGet]
        public IActionResult Book()
        {
            return View();
        }
    }
}
