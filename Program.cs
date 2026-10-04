using System.Text;
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
using ShiftingGuru.Services.Auth;
using ShiftingGuru.Services.Email;
using ShiftingGuru.Services.Notifications;
using ShiftingGuru.Services.Payments;
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
builder.Services.AddScoped<INotificationService, NotificationService>();

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
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;  // change to Always in production
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

if (string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience) || jwt.AccessTokenMinutes <= 0)
{
    throw new InvalidOperationException("Jwt:Issuer, Jwt:Audience and Jwt:AccessTokenMinutes must be set.");
}

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<ITokenService, JwtTokenService>();

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
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;  // Always in production
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

var app = builder.Build();

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