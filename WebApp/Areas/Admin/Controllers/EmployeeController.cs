using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using WebApp.Helpers;
using WebApp.Services;
using WebApp.Models.Auth;
using WebApp.Models;

namespace WebApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class EmployeeController : Controller
        
    {
        private readonly HttpClient _httpClient;
        private readonly SecurityClient _securityClient;
        private readonly OracleClientHelper _OracleClientHelper;

        public EmployeeController(IHttpClientFactory httpClientFactory, OracleClientHelper _oracleClientHelper, SecurityClient securityClient)
        {
            _httpClient = httpClientFactory.CreateClient("WebApiClient");
            _OracleClientHelper = _oracleClientHelper;
            _securityClient = securityClient;
        }
        [HttpGet]
        public IActionResult ChangePassword()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (string.IsNullOrEmpty(token))
            {
                TempData["Error"] = "Vui lòng đăng nhập trước.";
                return RedirectToAction("Login");
            }

            return View(new CustomerChangePasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(CustomerChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            try
            {
                // Lấy thông tin từ session
                var username = HttpContext.Session.GetString("Username");
                var platform = HttpContext.Session.GetString("Platform") ?? "WEB";
                if (string.IsNullOrWhiteSpace(username))
                {
                    ModelState.AddModelError(string.Empty, "Vui lòng đăng nhập trước.");
                    return View(model);
                }

                string client_id = "admin-"+username + platform;

                // Lấy private key từ session
                string? existingPrivateKeyPem = null;
                var privateKeyBase64 = HttpContext.Session.GetString("PrivateKeyBase64");
                if (!string.IsNullOrWhiteSpace(privateKeyBase64))
                {
                    existingPrivateKeyPem = Encoding.UTF8.GetString(Convert.FromBase64String(privateKeyBase64));
                }

                // Initialize SecurityClient với private key từ session
                await _securityClient.InitializeAsync(
                    HttpContext.RequestServices.GetService<IConfiguration>()!["WebApi:BaseUrl"]!,
                    client_id,
                    existingPrivateKeyPem);

                // Set headers cho authenticated request
                var token = HttpContext.Session.GetString("JwtToken");
                var sessionId = HttpContext.Session.GetString("SessionId");
                if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(sessionId))
                {
                    ModelState.AddModelError(string.Empty, "Vui lòng đăng nhập trước.");
                    return View(model);
                }
                _securityClient.SetHeaders(token, username, platform, sessionId);

                var payload = new
                {
                    OldPassword = model.OldPassword,
                    NewPassword = model.NewPassword
                };

                // Sử dụng endpoint mới: gửi encrypted request và nhận encrypted response
                // PostEncryptedAndGetEncryptedAsync sẽ tự động giải mã và trả về Data từ ApiResponse
                await _securityClient.PostEncryptedAndGetEncryptedAsync<object, string>(
                    "api/Admin/Employee/change-password/secure", payload);

                TempData["Message"] = "Đổi mật khẩu thành công.";
                return RedirectToAction("Index", "Home", new { area = "Admin" });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Lỗi kết nối API: " + ex.Message);
                return View(model);
            }
        }
        // --- Index: lấy danh sách nhân viên ---
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {

                var response = await _httpClient.GetAsync("api/Admin/Employee");

                if (response.IsSuccessStatusCode)
                {
                    var employees = await response.Content.ReadFromJsonAsync<List<EmployeeDto>>();
                    return View(employees);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    // Session Oracle bị kill → redirect login
                    TempData["Error"] = "Phiên làm việc hết hạn, vui lòng đăng nhập lại.";
                    HttpContext.Session.Clear();
                    return RedirectToAction("Login", "Employee", new { area = "Admin" });
                }
                else
                {
                    TempData["Error"] = "Không thể tải danh sách nhân viên: " + response.ReasonPhrase;
                    return View(new List<EmployeeDto>());
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi kết nối API: " + ex.Message;
                return View(new List<EmployeeDto>());
            }
        }

        
        // --- Login ---
        [HttpGet]
        public IActionResult Login() => View();

        private bool IsEmployeeLoggedIn()
        {
            var token = HttpContext.Session.GetString("JwtToken");
            return !string.IsNullOrEmpty(token);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Username và password không được để trống.");
                return View();
            }

            try
            {
                // Hardcode platform
                string platform = "WEB"; // hoặc "MOBILE" nếu mobile app

                var loginData = new
                {
                    Username = username,
                    Password = password,
                    Platform = platform
                };
                string client_id = username + platform;

                // Kiểm tra xem có private key trong session không
                string? existingPrivateKeyPem = null;
                var privateKeyBase64 = HttpContext.Session.GetString("PrivateKeyBase64");
                if (!string.IsNullOrWhiteSpace(privateKeyBase64))
                {
                    // Decode từ Base64 về PEM
                    existingPrivateKeyPem = Encoding.UTF8.GetString(Convert.FromBase64String(privateKeyBase64));
                }
                
                // Login: sử dụng private key từ session nếu có, nếu không thì generate mới
                await _securityClient.InitializeAsync(
                    HttpContext.RequestServices.GetService<IConfiguration>()!["WebApi:BaseUrl"]!, 
                    client_id, 
                    existingPrivateKeyPem);
                
                // Sử dụng endpoint mới: gửi encrypted request và nhận encrypted response
                var apiResult = await _securityClient.PostEncryptedAndGetEncryptedAsync<object, LoginResultEnvelope>(
                    "api/Admin/Employee/login/secure", loginData);

                if (apiResult.Result == "SUCCESS")
                {
                    HttpContext.Session.SetString("JwtToken", apiResult.Token ?? "");
                    HttpContext.Session.SetString("Username", apiResult.Username ?? "");
                    HttpContext.Session.SetString("Role", apiResult.Roles ?? "");
                    HttpContext.Session.SetString("Platform", platform);
                    HttpContext.Session.SetString("SessionId", apiResult.SessionId ?? "");
                    
                    // Kiểm tra xem đã có private key chưa, nếu chưa thì set flag để hiển thị modal bắt buộc
                    var hasPrivateKey = HttpContext.Session.GetString("PrivateKeyBase64");
                    if (string.IsNullOrWhiteSpace(hasPrivateKey))
                    {
                        // Lưu private key đã generate tạm thời vào session (để dùng cho request này)
                        // Nhưng vẫn yêu cầu user upload private key chính thức
                        var tempPrivateKeyPem = _securityClient.GetPrivateKeyPem();
                        if (!string.IsNullOrWhiteSpace(tempPrivateKeyPem))
                        {
                            HttpContext.Session.SetString("PrivateKeyBase64", Convert.ToBase64String(Encoding.UTF8.GetBytes(tempPrivateKeyPem)));
                        }
                        // Set flag để hiển thị modal bắt buộc
                        TempData["ShowUploadPrivateKeyModal"] = "true";
                    }

                    TempData["Message"] = $"Login thành công! ({apiResult.Roles})";
                    return RedirectToAction("Index", "Home", new { area = "Admin" });
                }
                ModelState.AddModelError("", $"Login thất bại: {apiResult.Result} - {apiResult.Message}");
                return View();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi kết nối API: " + ex.Message);
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CompleteQrLogin([FromBody] QrLoginCompleteDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Token))
            {
                return BadRequest(new { message = "Invalid QR login payload." });
            }

            HttpContext.Session.SetString("JwtToken", dto.Token);
            HttpContext.Session.SetString("Username", dto.Username);
            HttpContext.Session.SetString("Role", dto.Roles ?? string.Empty);
            HttpContext.Session.SetString("Platform", "WEB");
            HttpContext.Session.SetString("SessionId", dto.SessionId ?? Guid.NewGuid().ToString());

            TempData["Message"] = "Đăng nhập Admin bằng QR thành công.";

            var redirectUrl = Url.Action("Index", "Home", new { area = "Admin" });
            return Ok(new { redirect = redirectUrl });
        }

        [HttpGet]
        public IActionResult MobileQrLogin()
        {
            if (!IsEmployeeLoggedIn())
            {
                TempData["Error"] = "Vui lòng đăng nhập Admin Web trước.";
                return RedirectToAction("Login");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMobileQrSession()
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            var response = await _httpClient.PostAsync("api/Public/WebToMobileQr/create", null);
            var payload = await response.Content.ReadFromJsonAsync<WebApiResponse<WebToMobileQrCreateResponse>>();
            if (payload == null)
                return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());

            return Json(payload);
        }

        [HttpGet]
        public async Task<IActionResult> MobileQrStatus(string qrLoginId)
        {
            if (string.IsNullOrWhiteSpace(qrLoginId))
                return BadRequest(new { message = "qrLoginId is required." });

            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;

            var response = await _httpClient.GetAsync($"api/Public/WebToMobileQr/status/{qrLoginId}");
            var payload = await response.Content.ReadFromJsonAsync<WebApiResponse<WebToMobileQrStatusResponse>>();
            if (payload == null)
                return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());

            return Json(payload);
        }


        // --- Logout ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {
               await _httpClient.PostAsync("api/Admin/Employee/logout", null);

                // Chỉ xóa session liên quan đến Admin
                _OracleClientHelper.ClearSession();

                TempData["Message"] = "Logout thành công!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi logout: " + ex.Message;
            }

            return RedirectToAction("Login", "Employee", new { area = "Admin" });
        }

        // --- Register ---
        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(EmployeeRegisterDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            try
            {
                // Lấy thông tin từ session
                var username = HttpContext.Session.GetString("Username");
                var platform = HttpContext.Session.GetString("Platform") ?? "WEB";
                if (string.IsNullOrWhiteSpace(username))
                {
                    ModelState.AddModelError("", "Vui lòng đăng nhập trước.");
                    return View(dto);
                }
                string client_id = "admin-" + username + platform;

                // Lấy private key từ session
                string? existingPrivateKeyPem = null;
                var privateKeyBase64 = HttpContext.Session.GetString("PrivateKeyBase64");
                if (string.IsNullOrWhiteSpace(privateKeyBase64))
                {
                    ModelState.AddModelError("", "Vui lòng upload private key trước khi sử dụng chức năng này.");
                    return View(dto);
                }
                existingPrivateKeyPem = Encoding.UTF8.GetString(Convert.FromBase64String(privateKeyBase64));

                // Initialize SecurityClient với private key từ session
                await _securityClient.InitializeAsync(
                    HttpContext.RequestServices.GetService<IConfiguration>()!["WebApi:BaseUrl"]!,
                    client_id,
                    existingPrivateKeyPem);

                // Set headers cho authenticated request
                var token = HttpContext.Session.GetString("JwtToken");
                var sessionId = HttpContext.Session.GetString("SessionId");
                if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(sessionId))
                {
                    ModelState.AddModelError("", "Vui lòng đăng nhập trước.");
                    return View(dto);
                }
                _securityClient.SetHeaders(token, username, platform, sessionId);

                // Sử dụng endpoint mới: gửi encrypted request và nhận encrypted response
                var responseObj = await _securityClient.PostEncryptedAndGetEncryptedAsync<EmployeeRegisterDto, RegisterSecureResponse>(
                    "api/admin/employee/register/secure", dto);

                // Xử lý response
                if (responseObj != null && responseObj.Success)
                {
                    if (responseObj.Type == "file" && !string.IsNullOrWhiteSpace(responseObj.PrivateKeyBase64))
                    {
                        // Giải mã private key từ base64
                        byte[] privateKeyBytes = Convert.FromBase64String(responseObj.PrivateKeyBase64);
                        string fileName = responseObj.FileName ?? $"{dto.Username}_private_key.txt";

                        // Trả file cho user tải về
                        return File(privateKeyBytes, "text/plain", fileName);
                    }
                    else
                    {
                        ModelState.AddModelError("", $"Đăng ký thành công nhưng không nhận được private key: {responseObj?.Message ?? "Response không hợp lệ"}");
                        return View(dto);
                    }
                }
                else
                {
                    ModelState.AddModelError("", $"Đăng ký thất bại: {responseObj?.Message ?? "Response không hợp lệ"}");
                    return View(dto);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Lỗi kết nối API: {ex.Message}");
                return View(dto);
            }
        }


        // --- Unlock/Lock ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                TempData["Error"] = "Username không được để trống.";
                return RedirectToAction("Index");
            }
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/Admin/Employee/unlock", new { Username = username });
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<EmployeeUnlockResponse>();
                    TempData["Message"] = $"Unlock tài khoản '{username}': {result.Result}";
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Unlock thất bại: {error}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi kết nối API: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lock(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                TempData["Error"] = "Username không được để trống.";
                return RedirectToAction("Index");
            }
            if (!_OracleClientHelper.TrySetHeaders(_httpClient, out var redirect))
                return redirect;
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/Admin/Employee/lock", new { Username = username });
                if (response.IsSuccessStatusCode)
                {
                    TempData["Message"] = $"Lock tài khoản '{username}' thành công.";
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    TempData["Error"] = $"Lock thất bại: {error}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi kết nối API: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        // --- Upload Private Key ---
        [HttpPost]
        public async Task<IActionResult> UploadPrivateKey(IFormFile privateKeyFile)
        {
            try
            {
                if (privateKeyFile == null || privateKeyFile.Length == 0)
                {
                    return Json(new { success = false, message = "Vui lòng chọn file private key." });
                }

                // Đọc nội dung file - phát hiện encoding tự động
                string privateKeyContent;
                byte[] fileBytes;
                using (var ms = new MemoryStream())
                {
                    await privateKeyFile.CopyToAsync(ms);
                    fileBytes = ms.ToArray();
                }

                // Thử detect encoding: UTF-8 hoặc UTF-16
                // Nếu có BOM UTF-16, dùng UTF-16, ngược lại thử UTF-8 trước
                if (fileBytes.Length >= 2 && fileBytes[0] == 0xFF && fileBytes[1] == 0xFE)
                {
                    // UTF-16 LE BOM
                    privateKeyContent = Encoding.Unicode.GetString(fileBytes, 2, fileBytes.Length - 2);
                }
                else if (fileBytes.Length >= 2 && fileBytes[0] == 0xFE && fileBytes[1] == 0xFF)
                {
                    // UTF-16 BE BOM
                    privateKeyContent = Encoding.BigEndianUnicode.GetString(fileBytes, 2, fileBytes.Length - 2);
                }
                else if (fileBytes.Length >= 3 && fileBytes[0] == 0xEF && fileBytes[1] == 0xBB && fileBytes[2] == 0xBF)
                {
                    // UTF-8 BOM
                    privateKeyContent = Encoding.UTF8.GetString(fileBytes, 3, fileBytes.Length - 3);
                }
                else
                {
                    // Không có BOM, thử UTF-8 trước
                    try
                    {
                        privateKeyContent = Encoding.UTF8.GetString(fileBytes);
                        // Kiểm tra xem có ký tự null (\0) không - nếu có thì có thể là UTF-16
                        if (privateKeyContent.Contains('\0'))
                        {
                            // Có thể là UTF-16 không có BOM
                            privateKeyContent = Encoding.Unicode.GetString(fileBytes);
                        }
                    }
                    catch
                    {
                        // Fallback: thử UTF-16
                        privateKeyContent = Encoding.Unicode.GetString(fileBytes);
                    }
                }

                string normalizedPrivateKey = privateKeyContent.Trim();
                string privateKeyBase64;

                // Kiểm tra xem là PEM format hay Base64 thuần
                if (normalizedPrivateKey.Contains("BEGIN") || normalizedPrivateKey.Contains("END"))
                {
                    // Đã là PEM format
                    // Validate private key format (kiểm tra có thể đọc được không)
                    try
                    {
                        using (var rsa = RSA.Create())
                        {
                            rsa.ImportFromPem(normalizedPrivateKey);
                            // Chỉ validate, không extract public key
                        }
                    }
                    catch (Exception ex)
                    {
                        return Json(new { success = false, message = $"Không thể đọc private key PEM: {ex.Message}" });
                    }
                    // Lưu PEM vào session (encode Base64)
                    privateKeyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(normalizedPrivateKey));
                }
                else
                {
                    // Là Base64 thuần, cần làm sạch và convert sang PEM format
                    // Loại bỏ whitespace, newlines, và các ký tự không hợp lệ
                    string cleanedBase64 = normalizedPrivateKey
                        .Replace("\r", "")
                        .Replace("\n", "")
                        .Replace(" ", "")
                        .Replace("\t", "");
                    
                    // Kiểm tra Base64 hợp lệ
                    if (string.IsNullOrWhiteSpace(cleanedBase64))
                    {
                        return Json(new { success = false, message = "Private key không hợp lệ (rỗng sau khi làm sạch)." });
                    }

                    try
                    {
                        byte[] keyBytes = Convert.FromBase64String(cleanedBase64);
                        
                        // Thử import dưới dạng PKCS8
                        using (var rsa = RSA.Create())
                        {
                            rsa.ImportPkcs8PrivateKey(keyBytes, out _);
                            // Nếu thành công, export lại dưới dạng PEM
                            byte[] pkcs8Bytes = rsa.ExportPkcs8PrivateKey();
                            normalizedPrivateKey = "-----BEGIN PRIVATE KEY-----\n" + 
                                Convert.ToBase64String(pkcs8Bytes) + 
                                "\n-----END PRIVATE KEY-----";
                        }
                    }
                    catch
                    {
                        // Nếu không phải PKCS8, thử RSAPrivateKey format
                        try
                        {
                            byte[] keyBytes = Convert.FromBase64String(cleanedBase64);
                            using (var rsa = RSA.Create())
                            {
                                rsa.ImportRSAPrivateKey(keyBytes, out _);
                                // Export lại dưới dạng PEM PKCS8
                                byte[] pkcs8Bytes = rsa.ExportPkcs8PrivateKey();
                                normalizedPrivateKey = "-----BEGIN PRIVATE KEY-----\n" + 
                                    Convert.ToBase64String(pkcs8Bytes) + 
                                    "\n-----END PRIVATE KEY-----";
                            }
                        }
                        catch (Exception ex)
                        {
                            return Json(new { success = false, message = $"Không thể đọc private key Base64: {ex.Message}. Vui lòng kiểm tra format. (Đã làm sạch: {cleanedBase64.Substring(0, Math.Min(50, cleanedBase64.Length))}...)" });
                        }
                    }
                    
                    // Validate lại sau khi convert
                    try
                    {
                        using (var rsa = RSA.Create())
                        {
                            rsa.ImportFromPem(normalizedPrivateKey);
                        }
                    }
                    catch (Exception ex)
                    {
                        return Json(new { success = false, message = $"Không thể validate private key sau khi convert: {ex.Message}" });
                    }
                    
                    // Lưu PEM vào session (encode Base64)
                    privateKeyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(normalizedPrivateKey));
                }

                // Lưu private key vào session (Base64)
                HttpContext.Session.SetString("PrivateKeyBase64", privateKeyBase64);
                HttpContext.Session.SetString("PrivateKeyBase", privateKeyContent);

                return Json(new { success = true, message = "Upload private key thành công! Private key đã được lưu vào session." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }
    }
}
