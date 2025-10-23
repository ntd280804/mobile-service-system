using Microsoft.AspNetCore.Mvc;
using NuGet.Common;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using WebApp.Helpers;
namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class PartrequestController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly OracleClientHelper _OracleClientHelper;

        public PartrequestController(IHttpClientFactory httpClientFactory, OracleClientHelper _oracleClientHelper)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
            _OracleClientHelper = _oracleClientHelper;
        }
        // GET: /Admin/partrequest
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {

                var response = await _httpClient.GetAsync("api/admin/partrequest/getallpartrequest");
                if (!response.IsSuccessStatusCode)
                {
                    // Try to read error details from API response
                    var errorContent = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Không thể tải danh sách part request: {response.ReasonPhrase} - {errorContent}";
                    return View(new List<PartRequestViewModel>());
                }

                var list = await response.Content.ReadFromJsonAsync<List<PartRequestViewModel>>() ?? new List<PartRequestViewModel>();
                return View(list);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return View(new List<PartRequestViewModel>());
            }
        }
        public class PartRequestViewModel
        {
            public int REQUEST_ID { get; set; }
            public int ORDER_ID { get; set; }
            public string EmpUsername { get; set; }
            public DateTime REQUEST_DATE { get; set; }
            public string STATUS { get; set; }

        }
    }
}