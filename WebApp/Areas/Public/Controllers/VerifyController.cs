using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using WebApp.Models;

namespace WebApp.Areas.Public.Controllers
{
    [Area("Public")]
    public class VerifyController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public VerifyController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(IFormFile pdfFile)
        {
            if (pdfFile == null || pdfFile.Length == 0)
            {
                ViewData["Error"] = "Vui lòng chọn file PDF đã ký.";
                return View();
            }

            try
            {
                var baseUrl = (_configuration["WebApi:BaseUrl"] ?? string.Empty).TrimEnd('/');
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    ViewData["Error"] = "Chưa cấu hình WebApi:BaseUrl.";
                    return View();
                }

                using var content = new MultipartFormDataContent();
                var streamContent = new StreamContent(pdfFile.OpenReadStream());
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                content.Add(streamContent, "file", pdfFile.FileName);
                content.Add(new StringContent("0"), "invoiceId");

                var response = await _httpClient.PostAsync($"{baseUrl}/api/Public/Verify/verify-invoice", content);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    ViewData["Error"] = $"API trả về lỗi: {response.StatusCode} - {errorBody}";
                    return View();
                }

                var apiResult = await response.Content.ReadFromJsonAsync<WebApiResponse<bool>>();
                if (apiResult == null)
                {
                    ViewData["Error"] = "Không đọc được phản hồi từ API.";
                    return View();
                }

                if (!apiResult.Success)
                {
                    ViewData["Error"] = apiResult.Error ?? "API trả về lỗi.";
                    return View();
                }

                // Gắn cờ cho View để hiển thị đúng màu/icon theo kết quả kiểm tra
                var isMatch = apiResult.Data == true;
                ViewData["IsMatch"] = isMatch;

                ViewData["Result"] = isMatch
                    ? "PDF trùng khớp với bản lưu trong hệ thống."
                    : "PDF không trùng với bản lưu trong hệ thống.";
            }
            catch (Exception ex)
            {
                ViewData["Error"] = "Lỗi kết nối API: " + ex.Message;
            }

            return View();
        }
    }
}

