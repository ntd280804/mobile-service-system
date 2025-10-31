using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using WebApp.Models;
using WebApp.Services;

namespace WebApp.Areas.Public.Controllers
{
    [Area("Public")]
    public class SecurityDemoController : Controller
    {
        private readonly SecurityClient _securityClient;

        public SecurityDemoController(SecurityClient securityClient)
        {
            _securityClient = securityClient;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            await _securityClient.InitializeAsync(HttpContext.RequestServices.GetService<IConfiguration>()!["WebApi:BaseUrl"]!, "demo-client");
            return View(new SecurityDemoViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SecurityDemoViewModel model, string submit)
        {
            await _securityClient.InitializeAsync(HttpContext.RequestServices.GetService<IConfiguration>()!["WebApi:BaseUrl"]!, "demo-client");

            if (!ModelState.IsValid)
                return View(model);

            try
            {
                if (submit == "echo")
                {
                    model.EchoFromServer = await _securityClient.PostEncryptedEchoAsync(new { message = model.Message ?? string.Empty });
                }
                else if (submit == "encrypt-for-client")
                {
                    model.EncryptedFromServer = await _securityClient.GetEncryptedFromServerAsync(model.Message ?? string.Empty);
                }
            }
            catch (System.Exception ex)
            {
                model.Error = ex.Message;
            }

            return View(model);
        }
    }
}


