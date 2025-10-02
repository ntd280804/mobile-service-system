var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews();

// --- Thêm HttpClientFactory để gọi WebAPI ---
builder.Services.AddHttpClient("WebApiClient", client =>
{
    client.BaseAddress = new Uri("https://localhost:7179/"); // URL của WebAPI
    client.Timeout = TimeSpan.FromSeconds(30);
});

// --- Thêm session ---
builder.Services.AddDistributedMemoryCache(); // Lưu session trên RAM
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".MyApp.Session"; // tùy chọn tên cookie
});

// --- Nếu muốn HttpContext trong Helper/Service ---
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Public/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// --- Enable session ---
app.UseSession();

app.UseAuthorization();

// Route cho tất cả Area (Admin, Public,...)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// Route default, nếu truy cập / thì tự động vào Area Public
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}",
    defaults: new { area = "Public", controller = "Home", action = "Index" });

app.Run();
