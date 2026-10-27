using API.Areas.Admin.Models.RecaptchaUsage;
using API.Areas.Admin.Models.Services;
using API.Areas.Admin.Models.UserCategoryAccess;
using API.Filters;
using API.MiddleWares;
using API.Models;
using API.Models.ManagerFiles;
using API.Models.MyHelper;
using API.Models.WordPress;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using GoogleService = API.Models.MyHelper.GoogleService;


var builder = WebApplication.CreateBuilder(args);

// SET GLOBAL CONFIG FROM BUILDER
API.Startup._config = builder.Configuration;
API.Startup.Configuration = builder.Configuration;
API.Startup.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
API.Startup.ConnectionStringSTP = builder.Configuration.GetConnectionString("STPConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
API.Startup.ConnectionStringMysql = builder.Configuration.GetConnectionString("DefaultMySQL");
API.Startup.reCAPTCHASiteKey = builder.Configuration["reCAPTCHASiteKey"];
API.Startup.reCAPTCHASecretKey = builder.Configuration["reCAPTCHASecretKey"];

// Localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Filters, Razor, MVC, Newtonsoft
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(typeof(SampleActionFilter));
})
.AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
.AddDataAnnotationsLocalization()
.AddNewtonsoftJson(options =>
{
    options.SerializerSettings.ContractResolver = new DefaultContractResolver();
});

// Razor runtime compilation
var mvcBuilder = builder.Services.AddRazorPages();
builder.Services.AddRazorPages().AddRazorRuntimeCompilation();
if (builder.Environment.IsDevelopment())
{
    // mvcBuilder.AddRazorRuntimeCompilation();
}

builder.Services.AddHttpClient("WPApi", client =>
{
    client.BaseAddress = new Uri("https://ttytmdrak.vn/wp-json/wp/v2/");
    client.Timeout = TimeSpan.FromMinutes(3);
});

builder.Services.AddScoped<WordPressService>();

// Background Services
builder.Services.AddHostedService<TempFileCleanupService>();

builder.Services.AddHttpClient();

// Session
builder.Services.AddSession(options =>
{

    //options.IdleTimeout = TimeSpan.FromSeconds(3600000);
    options.IdleTimeout = TimeSpan.FromHours(8); // Tăng từ 60 phút lên 8 giờ để tránh tự logout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Form size limits
builder.Services.Configure<FormOptions>(x =>
{
    x.ValueLengthLimit = 937280000;
    x.MultipartBodyLengthLimit = 937280000;
    x.MultipartHeadersLengthLimit = 937280000;
});
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 83728000000;
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // Đảm bảo lấy đúng IP client khi chạy sau reverse proxy/nginx/iis.
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.Configure<BlacklistOptions>(
    builder.Configuration.GetSection(BlacklistOptions.SectionName));

// Kestrel: tăng timeout cho request dài (đăng bài có nhiều ảnh, nội dung lớn)
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(15);
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(15);
    // Tắt MinRequestBodyDataRate để cho phép upload chậm (form lớn)
    serverOptions.Limits.MinRequestBodyDataRate = null;
    serverOptions.Limits.MinResponseDataRate = null;
});

// Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "image/svg+xml" });
});

// Authentication & Authorization
builder.Services.AddAuthentication(authOptions =>
{
    authOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    authOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(bearerOptions =>
{
    var paramsValidation = bearerOptions.TokenValidationParameters;
    paramsValidation.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]));
    paramsValidation.ValidAudience = builder.Configuration["Jwt:Issuer"];
    paramsValidation.ValidIssuer = builder.Configuration["Jwt:Issuer"];
    paramsValidation.ValidateIssuerSigningKey = true;
    paramsValidation.ValidateLifetime = true;
    paramsValidation.ClockSkew = TimeSpan.Zero;
});

builder.Services.AddAuthorization(auth =>
{
    auth.AddPolicy("Bearer", new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser().Build());
});

builder.Services.AddDistributedMemoryCache();

// Add by Tayd - reCAPTCHA v3 sites PYN - START

// Đăng ký IHttpContextAccessor (built-in)
builder.Services.AddHttpContextAccessor();
// Đăng ký IRecaptchaQuotaService RecaptchaQuotaService
builder.Services.AddScoped<IRecaptchaQuotaService, RecaptchaQuotaService>();
// Đăng ký IUserCategoryAccessService UserCategoryAccessService
builder.Services.AddScoped<IUserCategoryAccessService, UserCategoryAccessService>();
// Đăng ký _google service (Scoped cho per-request)
builder.Services.AddScoped<IGoogleService, GoogleService>();
// Đăng ký BackgroundService (Hosted Service)
builder.Services.AddHostedService<RecaptchaResetService>();

// Thêm MemoryCache để đếm reject
builder.Services.AddMemoryCache();




builder.Services.AddRateLimiter(options =>
{
    // Policy 1: Contact form / hỏi đáp (chống spam POST)
    options.AddPolicy("contact", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"contact:{ip}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 6,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Policy 2: Admin login - tách GET/POST để tránh khóa oan khi chỉ refresh trang login.
    options.AddPolicy("admin_login", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var method = httpContext.Request.Method;
        var isPost = HttpMethods.IsPost(method);
        var permitLimit = isPost ? 6 : 30;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"admin_login:{ip}:{method}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Policy 3: Giới hạn chung cho toàn API (100 request / 1 phút mỗi IP)
    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
    });


    // Khi bị reject (vượt giới hạn)
    options.OnRejected = (context, token) =>
    {
        var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var policyName = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName ?? "default";
        BlacklistService.AddReject(ip, policyName);

        context.HttpContext.Response.StatusCode = 429; // Too Many Requests
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers["Retry-After"] = Math.Max(1, (int)retryAfter.TotalSeconds).ToString();
        }
        else
        {
            context.HttpContext.Response.Headers["Retry-After"] = "60";
        }
        context.HttpContext.Response.WriteAsync("Too many login attempts. Please try again later.");
        return ValueTask.CompletedTask;
    };
});

// Add by Tayd - reCAPTCHA v3 sites PYN  - END

var app = builder.Build();

// Inject và gán Cache cho BlacklistService
BlacklistService.Cache = app.Services.GetRequiredService<IMemoryCache>();


// BlacklistService dùng Cache
BlacklistService.LoadFromFile();

// MIDDLEWARE PIPELINE
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// Localization
//var supportedCultures = new[]
//{
//    new CultureInfo("vi"),
//    new CultureInfo("vi-VN"),
//    new CultureInfo("en"),
//    new CultureInfo("en-US")
//};

//app.UseHttpsRedirection();
//app.UseStaticFiles();
//app.UseCookiePolicy();
//app.UseRouting();
//app.UseSession();

// Localization should be after session but before authentication
//app.UseRequestLocalization(new RequestLocalizationOptions
//{
//    DefaultRequestCulture = new RequestCulture("vi"),
//    SupportedCultures = supportedCultures,
//    SupportedUICultures = supportedCultures,
//    RequestCultureProviders = new List<IRequestCultureProvider>
//    {
//        new CookieRequestCultureProvider(),
//        new AcceptLanguageHeaderRequestCultureProvider()
//    }
//});

// Localization
var supportedCultures = new[]
{
    new CultureInfo("vi"),
    new CultureInfo("vi-VN"),
    new CultureInfo("en"),
    new CultureInfo("en-US")
};

var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("vi-VN"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
    RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new CookieRequestCultureProvider(),
        new QueryStringRequestCultureProvider()
    }
};

//app.UseHttpsRedirection();
app.UseForwardedHeaders();
app.UseHttpsRedirection();

// Chặn System Information Leak: từ chối các URL rác (SEO Spam / scan tool)
// Trả về 404 để không lộ thông tin cấu trúc ứng dụng
// static readonly: khởi tạo 1 lần duy nhất, tái sử dụng cho mọi request
var blockedKeywords = new[]
{
    "casino", "baccarat", "slot", "?bet", "kubet", "m88", "fun88",
    "bong88", "bongda", "nhacaivip", "otp", "phimsex", "jav", "sex",
    "sneaws", "redirect-keyword", "seo-hack", "seo-tang-hang",
    "hidden-link", "google.sneaws", "ban-thuoc", "mua-thuoc",
    "gia-re", "?play", "?android", "?blog/", "?product/",
    "?collections", "?ios", "?bak", "?root", "?games", "?download",
    "?jogo", "?sim", "phpinfo", "eval(", "base64_decode",
    "union+select", "union select", "<script", "javascript:",
    "vbscript:", "../", "..\\"
};

// Chặn System Information Leak: từ chối các URL rác (SEO Spam / scan tool)
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
    var query = context.Request.QueryString.Value?.ToLowerInvariant() ?? "";
    var fullUrl = path + query;

    if (blockedKeywords.Any(k => fullUrl.Contains(k)))
    {
        context.Response.StatusCode = 404;
        return;
    }

    await next();
});

app.UseStaticFiles();
app.UseCookiePolicy();
app.UseRouting();
app.UseSession();

app.Use(async (context, next) =>
{
    context.Session.SetString("TemplateName", "Pbgd");
    await next();
});

app.Use(async (context, next) =>
{
    if (string.IsNullOrEmpty(context.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName]))
    {
        var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture("vi-VN"));
        context.Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName, cookieValue);
    }
    await next();
});

app.UseRequestLocalization(localizationOptions);

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMyAuthentication();
app.UseResponseCompression();



// ==================== MIDDLEWARE KIỂM TRA BLACKLIST - ĐÃ TỐI ƯU ====================
// Đặt ngay trước app.UseRateLimiter();
app.Use(async (context, next) =>
{
    var requestPath = context.Request.Path.Value?.ToLowerInvariant() ?? "";

    // === DANH SÁCH BỎ QUA (không kiểm tra blacklist) ===
    // Những request này chiếm phần lớn traffic và không cần kiểm tra
    if (API.Models.MyHelper.StringHelper.IsStaticOrExternalResource(requestPath, context))
    {
        await next();
        return;
    }

    // Chỉ kiểm tra blacklist cho các request động / quan trọng
    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    if (BlacklistService.IsBlacklisted(ip))
    {
        context.Response.StatusCode = 403;
        await context.Response.WriteAsync("IP của bạn bị chặn do hoạt động bất thường. Vui lòng liên hệ admin.");
        return;
    }

    await next();
});

app.UseRateLimiter();




// Security headers + Vary
app.Use(async (context, next) =>
{
    context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.Vary] =
        new string[] { "Accept-Encoding" };

    // Chống Cross-Frame Scripting (Clickjacking)
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";

    // Chống Overly Permissive Message Posting Policy:
    //context.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'self'";
    context.Response.Headers["Content-Security-Policy"] =
        "frame-ancestors 'self'; " +
        "default-src 'self' 'unsafe-inline' 'unsafe-eval' data: blob: https:; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https:; " +
        "connect-src 'self' https:;";

    // Các header bảo mật khác (đảm bảo hoạt động cả khi chạy Kestrel trực tiếp không qua IIS)
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

    await next();
});

// Route login admin → áp dụng policy SlidingWindow chặt chẽ
app.MapAreaControllerRoute(
    name: "admin_login",
    areaName: "admin",
    pattern: "/Admin/Account/Login",
    defaults: new { controller = "Account", action = "Login" })
.RequireRateLimiting("admin_login");

app.MapAreaControllerRoute(
    name: "admin",
    areaName: "admin",
    pattern: "Admin/{controller=Home}/{action=Index}/{id?}");

app.MapAreaControllerRoute(
    name: "api",
    areaName: "api",
    pattern: "api/{controller=Home}/{action=Index}/{id?}");

app.MapAreaControllerRoute(
    name: "image",
    areaName: "image",
    pattern: "Image/{controller=Home}/{action=Index}/{id?}");


app.MapControllerRoute(
    name: "TrangChu",
    pattern: "trang-chu",
    defaults: new { controller = "Home", action = "Index" });

app.MapControllerRoute(
    name: "DuThaoVanBan",
    pattern: "du-thao-van-ban.html",
    defaults: new { controller = "DuThaoVanBan", action = "Index" });

app.MapControllerRoute(
    name: "DanhSachHopTacXa",
    pattern: "danh-sach-hop-tac-xa.html",
    defaults: new { controller = "HTX", action = "Index" });

app.MapControllerRoute(
    name: "Albums",
    pattern: "albums.html",
    defaults: new { controller = "Albums", action = "Index" });

app.MapControllerRoute(
    name: "Audios_Detail",
    pattern: "truyen-thanh/{alias}-{id}.html",
    defaults: new { controller = "Audios", action = "Detail" });

app.MapControllerRoute(
    name: "CategoriesAudios",
    pattern: "loai-truyen-thanh/{alias}-{id}.html",
    defaults: new { controller = "Audios", action = "Index" });

app.MapControllerRoute(
    name: "Videos_Detail",
    pattern: "video/{alias}-{id}.html",
    defaults: new { controller = "Videos", action = "Detail" });

app.MapControllerRoute(
    name: "CategoriesVideos",
    pattern: "loai-videos/{alias}-{id}.html",
    defaults: new { controller = "Videos", action = "Index" });

app.MapControllerRoute(
    name: "VideosId",
    pattern: "videos.html",
    defaults: new { controller = "Videos", action = "Index", Id = 1, Alias = "videos" });

app.MapControllerRoute(
    name: "Contacts_LienHe",
    pattern: "lien-he.html",
    defaults: new { controller = "Contacts", action = "Detail" })
    .RequireRateLimiting("contact"); //Giới hạn chặt cho các form liên hệ / hỏi đáp (ngăn spam)

app.MapControllerRoute(
    name: "Contacts",
    pattern: "hoi-dap.html",
    defaults: new { controller = "Contacts", action = "Index" })
    .RequireRateLimiting("contact"); //Giới hạn chặt cho các form liên hệ / hỏi đáp (ngăn spam)

app.MapControllerRoute(
    name: "LegalQA",
    pattern: "hoi-dap-phap-luat.html",
    defaults: new { controller = "LegalQA", action = "Index" })
    .RequireRateLimiting("contact");

app.MapControllerRoute(
    name: "Contacts",
    pattern: "tra-loi-hoi-dap.html",
    defaults: new { controller = "Contacts", action = "List" });

app.MapControllerRoute(
    name: "LegalQA_List",
    pattern: "tra-loi-hoi-dap-phap-luat.html",
    defaults: new { controller = "LegalQA", action = "List" });

app.MapControllerRoute(
    name: "TraCuuUsers",
    pattern: "tra-cuu-thong-tin.html",
    defaults: new { controller = "Users", action = "Index" });

app.MapGet("/tra-cuu-dia-diem-bau-cu.html", () => Results.Redirect("/tra-cuu-dia-diem-bau-cu/index.html", permanent: false));
app.MapGet("/tra-cuu-thong-tin-bau-cu-tan-lap.html", () => Results.Redirect("/tra-cuu-thong-tin-bau-cu-tan-lap/index.html", permanent: false));

app.MapControllerRoute(
    name: "HopTrucTuyen",
    pattern: "hop-truc-tuyen.html",
    defaults: new { controller = "Account", action = "HopTrucTuyen" });

app.MapControllerRoute(
    name: "HopTrucTuyenTaiLieu",
    pattern: "hop-truc-tuyen/tai-lieu.html",
    defaults: new { controller = "HopTrucTuyen", action = "TaiLieu" });

app.MapControllerRoute(
    name: "LichCongTac",
    pattern: "lich-cong-tac.html",
    defaults: new { controller = "WorkSchedules", action = "Index" });

app.MapControllerRoute(
    name: "Contacts",
    pattern: "cau-hoi-duoc-tra-loi.html",
    defaults: new { controller = "Contacts", action = "List" });

app.MapControllerRoute(
    name: "LegalQA_Answered",
    pattern: "cau-hoi-phap-luat-duoc-tra-loi.html",
    defaults: new { controller = "LegalQA", action = "List" });

app.MapControllerRoute(
    name: "Reviews",
    pattern: "danh-gia-website.html",
    defaults: new { controller = "Reviews", action = "Index" });

app.MapControllerRoute(
    name: "Products_Categories",
    pattern: "loai-san-pham/{alias}-{id}.html",
    defaults: new { controller = "Products", action = "Index" });

app.MapControllerRoute(
    name: "Products_Detail",
    pattern: "san-pham/{alias}-{id}.html",
    defaults: new { controller = "Products", action = "Detail" });

app.MapControllerRoute(
    name: "SiteMap",
    pattern: "sitemap.html",
    defaults: new { controller = "Home", action = "SiteMap" });

app.MapControllerRoute(
    name: "Sitemap",
    pattern: "sitemap.xml",
    defaults: new { controller = "Sitemap", action = "Index" });

app.MapControllerRoute(
    name: "ThuTucHanhChinhXa",
    pattern: "thu-tuc-hanh-chinh-cap-xa.html",
    defaults: new { controller = "Home", action = "ThuTucHanhChinhXa" });

app.MapControllerRoute(
    name: "ThuTucHanhChinh",
    pattern: "thu-tuc-hanh-chinh.html",
    defaults: new { controller = "Home", action = "ThuTucHanhChinh" });

app.MapControllerRoute(
    name: "ThuTucHanhChinhHuyen",
    pattern: "thu-tuc-hanh-chinh-cap-huyen.html",
    defaults: new { controller = "Home", action = "ThuTucHanhChinhHuyen" });

app.MapControllerRoute(
    name: "Articles",
    pattern: "articles/{alias}.html",
    defaults: new { controller = "Articles", action = "Index" });

app.MapControllerRoute(
    name: "ArticlesCategories",
    pattern: "categories/{alias}-{id}.html",
    defaults: new { controller = "Articles", action = "GetByCat" });

app.MapControllerRoute(
    name: "Documents_Detail",
    pattern: "van-ban/{alias}-{id}.html",
    defaults: new { controller = "Documents", action = "Detail" });

app.MapControllerRoute(
    name: "Articles",
    pattern: "thong-bao.html",
    defaults: new { controller = "Articles", action = "Notification" });

app.MapControllerRoute(
    name: "Articles",
    pattern: "tin-noi-bat.html",
    defaults: new { controller = "Articles", action = "Featured" });

app.MapControllerRoute(
    name: "CategoriesArticlesGetListChildCat",
    pattern: "listchildcat/{alias}-{id}.html",
    defaults: new { controller = "CategoriesArticles", action = "GetListChildCat" });

app.MapControllerRoute(
    name: "CategoriesArticlesListChildCatNoImage",
    pattern: "ListChildCatNoImage/{alias}-{id}.html",
    defaults: new { controller = "CategoriesArticles", action = "ListChildCatNoImage" });

app.MapControllerRoute(
    name: "RSS",
    pattern: "rss",
    defaults: new { controller = "Rss", action = "Index" });

app.MapControllerRoute(
    name: "RSS_GetByCat",
    pattern: "rss/{alias}-{id}.rss",
    defaults: new { controller = "Rss", action = "GetByCat" });

app.MapControllerRoute(
    name: "Api",
    pattern: "api",
    defaults: new { controller = "Api", action = "Index" });
app.MapControllerRoute(
    name: "Api_GetByCat",
    pattern: "api/{alias}-{id}.json",
    defaults: new { controller = "Api", action = "GetByCat" });

app.MapControllerRoute(
    name: "DocumentRefers",
    pattern: "tai-lieu-tham-khao.html",
    defaults: new { controller = "DocumentRefers", action = "Index" });

app.MapControllerRoute(
    name: "DocumentForms",
    pattern: "mau-bieu-hanh-chinh.html",
    defaults: new { controller = "DocumentForms", action = "Index" });

app.MapControllerRoute(
    name: "DocumentImages",
    pattern: "Documents/{folderid}/{filenameid}/{filetitle}/{uid}",
    defaults: new { controller = "Documents", action = "DongBoCode" });

app.MapControllerRoute(
    name: "Documents",
    pattern: "van-ban.html",
    defaults: new { controller = "Documents", action = "Index" });



app.MapControllerRoute(
    name: "BanLanhDao",
    pattern: "ban-lanh-dao.html",
    defaults: new { controller = "Users", action = "BanLanhDao" });

// FAQ – đặt route trước các route alias chung để không bị bắt vào Home/AliasDetail
app.MapControllerRoute(
    name: "FAQ",
    pattern: "faq",
    defaults: new { controller = "FAQ", action = "Index" });

app.MapControllerRoute(
    name: "FAQ_Html",
    pattern: "faq.html",
    defaults: new { controller = "FAQ", action = "Index" });

app.MapControllerRoute(
    name: "Articles_Detail",
    pattern: "{alias}-{id}.html",
    defaults: new { controller = "Articles", action = "Detail" });

app.MapControllerRoute(
    name: "Articles_Detail",
    pattern: "-/{alias}",
    defaults: new { controller = "Home", action = "AliasDetail" });

app.MapControllerRoute(
    name: "Articles_Detail",
    pattern: "{alias}",
    defaults: new { controller = "Home", action = "AliasDetail" });



app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
