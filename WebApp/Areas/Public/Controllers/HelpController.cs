using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebApp.Models;

namespace WebApp.Areas.Public.Controllers
{
    [Area("Public")]
    public class HelpController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HelpController> _logger;

        public HelpController(IHttpClientFactory httpClientFactory, ILogger<HelpController> logger)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendEmail(string email, string content)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(content))
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ email và nội dung.";
                return RedirectToAction("Index");
            }

            try
            {
                var request = new
                {
                    Email = email,
                    Content = content
                };

                var response = await _httpClient.PostAsJsonAsync("api/Public/Help/send-help-email", request);
                var payload = await response.Content.ReadFromJsonAsync<WebApiResponse<string>>();

                if (payload == null)
                {
                    TempData["Error"] = "Không thể gửi email. Vui lòng thử lại sau.";
                    return RedirectToAction("Index");
                }

                if (payload.Success)
                {
                    TempData["Message"] = payload.Data ?? "Email đã được gửi thành công! Chúng tôi sẽ phản hồi sớm nhất có thể.";
                }
                else
                {
                    TempData["Error"] = payload.Error ?? "Không thể gửi email.";
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi email trợ giúp");
                TempData["Error"] = $"Lỗi kết nối API: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public IActionResult GetFaqs()
        {
            try
            {
                var faqPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "data", "faq.json");
                if (System.IO.File.Exists(faqPath))
                {
                    var jsonContent = System.IO.File.ReadAllText(faqPath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var faqData = JsonSerializer.Deserialize<FaqData>(jsonContent, options);
                    return Json(faqData);
                }
                return Json(new FaqData { Faqs = new List<FaqItem>() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đọc FAQ: {Error}", ex.Message);
                return Json(new FaqData { Faqs = new List<FaqItem>() });
            }
        }

        public class FaqData
        {
            [JsonPropertyName("faqs")]
            public List<FaqItem> Faqs { get; set; } = new();
        }

        public class FaqItem
        {
            [JsonPropertyName("question")]
            public string Question { get; set; } = string.Empty;
            
            [JsonPropertyName("answer")]
            public string Answer { get; set; } = string.Empty;
        }
    }
}

