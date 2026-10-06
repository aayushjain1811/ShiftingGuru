using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ShiftingGuru.Controllers;
using ShiftingGuru.Data;
using ShiftingGuru.Services;
using ShiftingGuru.Services.Api;
using ShiftingGuru.Services.Auth;
using ShiftingGuru.Services.Email;
using ShiftingGuru.Services.Notifications;
using ShiftingGuru.Services.Payments;
using ShiftingGuru.Services.Places;
using ShiftingGuru.Services.Push;
using ShiftingGuru.Services.Seo;
using ShiftingGuru.Services.Storage;
using ShiftingGuru.Services.Verification;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------
// Services
// ---------------------------------------------------------------
builder.Services.AddControllersWithViews();

// Service catalog holds a fixed in-memory list, so one instance is enough.
builder.Services.AddSingleton<IServiceCatalog, StaticServiceCatalog>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ILeadService, LeadService>();
builder.Services.AddScoped<IPartnerService, PartnerService>();
builder.Services.AddScoped<ILeadAssignmentService, LeadAssignmentService>();
builder.Services.AddScoped<IQuoteService, QuoteService>();
builder.Services.AddScoped<ICustomerAccessService, CustomerAccessService>();
builder.Services.AddScoped<IQuoteSelectionService, QuoteSelectionService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<ISeoService, SeoService>();
builder.Services.AddHttpContextAccessor();       // AuditService needs it
builder.Services.AddScoped<IAuditService, AuditService>();

// NEW: email one-time codes for the partner sign-up form.
builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();

// NEW: Razorpay, for the partner registration fee. The secret key and webhook
// secret come from user secrets locally and Secret Manager on Cloud Run.
builder.Services.Configure<RazorpayOptions>(
    builder.Configuration.GetSection(RazorpayOptions.SectionName));
builder.Services.AddHttpClient<IRazorpayClient, RazorpayClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddScoped<IRegistrationPaymentService, RegistrationPaymentService>();

// NEW: checks the mobile proof with Firebase. One instance is enough; it sets
// Firebase up the first time it's needed.
builder.Services.AddSingleton<IPhoneVerificationService, FirebasePhoneVerificationService>();

// NEW: where partner documents are stored. A local folder while developing,
// a private Google Cloud Storage bucket in production.
builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection(StorageOptions.SectionName));

if (string.Equals(builder.Configuration["Storage:Provider"], "Gcs", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IDocumentStorage, GcsDocumentStorage>();
}
else if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IDocumentStorage, LocalDocumentStorage>();
}
else
{
    // Cloud Run wipes its disk on every restart, so a local folder there would
    // silently lose partners' documents. Refuse to start instead.
    throw new InvalidOperationException(
        "Partner documents need Storage:Provider=Gcs and Storage:Bucket outside Development.");
}


// ---------------------------------------------------------------
// Email and notifications
// ---------------------------------------------------------------
builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<AppOptions>(
    builder.Configuration.GetSection(AppOptions.SectionName));

// EmailTemplate takes AppOptions directly rather than IOptions, so unwrap it once.
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<AppOptions>>().Value);

builder.Services.AddSingleton<EmailTemplate>();
// Resend over HTTPS rather than SMTP: Cloud Run blocks port 25 and throttles
// the others. AddHttpClient pools connections and handles DNS changes, which
// a bare "new HttpClient()" does not.
builder.Services.AddHttpClient<IEmailService, ResendEmailService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
// CHANGED (mobile apps): NotificationService still sends every email exactly
// as before; PushingNotificationService wraps it and also sends the matching
// push notification to the partner's or customer's phones.
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<INotificationService, PushingNotificationService>();

// NEW (mobile apps): city search with Google Places. The key stays on the
// server; without one, the app simply lets people type the city.
builder.Services.Configure<GoogleMapsOptions>(builder.Configuration.GetSection(GoogleMapsOptions.SectionName));
builder.Services.AddHttpClient<IPlacesClient, GooglePlacesClient>(client =>
{
    client.BaseAddress = new Uri("https://places.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(8);
});

// NEW (mobile apps): push notifications through Expo's push service.
builder.Services.AddHttpClient<IPushSender, ExpoPushSender>(client =>
{
    client.BaseAddress = new Uri("https://exp.host/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

// The queue is process-wide; the worker drains it. Notifications are written
// to the database before being queued, so nothing is lost on a restart.
builder.Services.AddSingleton<NotificationQueue>();
builder.Services.AddHostedService<NotificationSender>();

// NEW: the keys that sign login cookies and form tokens are stored in the
// database, so every Cloud Run instance shares them and they survive restarts
// and deploys. Without this, users are logged out at random and forms can
// fail with a 400 when a different instance answers the POST.
builder.Services.AddDataProtection()
    .SetApplicationName("ShiftingGuru")
    .PersistKeysToDbContext<ApplicationDbContext>();

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    // CHANGED: simpler rule - at least 8 characters including a number.
    // Capitals and symbols are allowed but not required.
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Admin and partner cookie (Identity).
builder.Services.ConfigureApplicationCookie(options =>
{
    // Fallbacks. The handlers below pick the right door per area.
    options.LoginPath = "/admin/login";
    options.AccessDeniedPath = "/admin/denied";

    options.Cookie.HttpOnly = true;                                  // JavaScript can't read it
    options.Cookie.SameSite = SameSiteMode.Lax;                      // survives the login redirect
    // CHANGED: secure-only outside development, so the cookie never travels over plain http.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;

    // One cookie serves both admins and partners, so a single LoginPath would
    // send vendors to the admin sign-in page. Route by the path instead.
    options.Events.OnRedirectToLogin = context =>
    {
        var isPartner = context.Request.Path.StartsWithSegments("/partner");
        var target = isPartner ? "/partner/login" : "/admin/login";

        context.Response.Redirect(
            $"{target}?returnUrl={Uri.EscapeDataString(context.Request.Path + context.Request.QueryString)}");

        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        var isPartner = context.Request.Path.StartsWithSegments("/partner");
        context.Response.Redirect(isPartner ? "/partner/login" : "/admin/denied");
        return Task.CompletedTask;
    };
});

// ---------------------------------------------------------------
// NEW (mobile API): token sign-in for the mobile apps.
//
// Same Identity users, passwords and roles as the website. The website keeps
// its cookies; the apps get a signed token instead. Nothing here changes how
// the website signs anyone in.
// ---------------------------------------------------------------
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

// Refuse to start without a proper key, rather than run with a weak or empty
// one. Locally: dotnet user-secrets set "Jwt:SigningKey" "...".
// Cloud Run: the Jwt__SigningKey secret.
if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:SigningKey is missing or shorter than 32 characters. " +
        "Set it with user secrets locally and Secret Manager on Cloud Run.");
}

if (string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience) ||
    jwt.AccessTokenMinutes <= 0 || jwt.RefreshTokenDays <= 0)
{
    throw new InvalidOperationException(
        "Jwt:Issuer, Jwt:Audience, Jwt:AccessTokenMinutes and Jwt:RefreshTokenDays must be set.");
}

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<ITokenService, JwtTokenService>();

// NEW (mobile API): "stay signed in" tokens, stored in Identity's own
// AspNetUserTokens table - no new table, no migration.
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();

// NEW (mobile API): the partner profile the app shows after sign-in and on /me.
builder.Services.AddScoped<IPartnerProfileReader, PartnerProfileReader>();

// A second, separate cookie for customers. They have no password and are not
// Identity users - the access link signs them in and the cookie carries one
// claim: which lead they may see.
//
// IMPORTANT: AddAuthentication() stays EMPTY. Putting a scheme inside the
// brackets would change the default and break the website's cookie sign-in.
builder.Services.AddAuthentication()
    .AddCookie(CustomerPortalController.Scheme, options =>
    {
        options.Cookie.Name = "sg_customer";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // CHANGED: secure-only outside development.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.LoginPath = "/my-request/request-access";
    })
    // NEW (mobile API): only used by controllers that ask for it by name.
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        // Keep the short claim names ("sub", "role") exactly as written in
        // the token, instead of renaming them to long URLs.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,

            ValidateAudience = true,
            ValidAudience = jwt.Audience,

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),

            // So [Authorize(Roles = ...)] and User.Identity.Name read our claims.
            NameClaimType = ApiClaims.UserId,
            RoleClaimType = ApiClaims.Role
        };
    });

// ---------------------------------------------------------------
// NEW (security): Cloud Run sits behind Google's front end, which passes on
// the visitor's real address and "https" in X-Forwarded-* headers. Without
// reading them, every request looks like it came from Google over plain
// http - so rate limits would lump everyone together, and canonical links
// and secure cookies would be wrong. ForwardLimit = 1 (the default) trusts
// only the last entry, the one Google itself adds.
// ---------------------------------------------------------------
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// ---------------------------------------------------------------
// NEW (security): limits on the sign-in endpoints, per visitor address.
//
// Login already locks an ACCOUNT after 5 wrong passwords; this stops one
// address hammering many accounts, or the "is this number registered"
// checks, or flooding inboxes with reset codes. The rest of the API and the
// website are not limited.
//
// Generous on purpose: in India many mobile users share one address through
// their carrier, so a strict limit would block real people.
// ---------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var address = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (context.Request.Path.StartsWithSegments("/api/v1/auth"))
        {
            return RateLimitPartition.GetFixedWindowLimiter("auth:" + address, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,                    // 30 sign-in calls...
                Window = TimeSpan.FromMinutes(1),    // ...per minute, per address
                QueueLimit = 0 
            });
        }

        // NEW: city search costs money per call to Google, so it's limited too.
        if (context.Request.Path.StartsWithSegments("/api/v1/places"))
        {
            return RateLimitPartition.GetFixedWindowLimiter("places:" + address, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
        }

        return RateLimitPartition.GetNoLimiter("unlimited");
    });

    // The same JSON shape as every other API error, so the app shows a clear message.
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Too many attempts. Please wait a minute and try again.",
            code = "rateLimited"
        }, ct);
    };
});

var app = builder.Build();

// NEW (security): read the real visitor address and https first, before
// anything else looks at the request.
app.UseForwardedHeaders();

// ---------------------------------------------------------------
// Pipeline. Order matters: each line below runs in sequence for
// every request, so authentication must come before authorization.
// ---------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Re-executes the request against /error/404 while keeping the original
// status code. A 404 page served as 200 would let search engines index
// every broken URL.
//
// CHANGED (mobile API): skipped for /api. Without this, an empty 401 from
// the API would be replaced by the website's HTML error page, and the app
// would receive a web page instead of a status it can read.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api"),
    website => website.UseStatusCodePagesWithReExecute("/error/{0}"));

app.UseHttpsRedirection();
app.UseRouting();

app.UseRateLimiter();      // NEW: limits on the sign-in endpoints (see above)

app.UseAuthentication();   // reads the cookies (and API tokens) and builds User
app.UseAuthorization();    // checks User against [Authorize]

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Creates the Admin and Vendor roles, and the first admin user if
// SHIFTINGGURU_ADMIN_EMAIL and SHIFTINGGURU_ADMIN_PASSWORD are configured.
await AdminSeeder.SeedAsync(app.Services);

// Seven cities and five routes as DRAFTS. Nothing is published until an admin
// reviews the content, so /locations is empty on first run by design.
await SeoSeeder.SeedAsync(app.Services);
await CityCatalogSeeder.SeedAsync(app.Services);

app.Run();