using System.Globalization;
using System.Text;
using AspBaseProj.Application;
using AspBaseProj.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

// Application
builder.Services.AddApplication();

// Controllers
builder.Services.AddControllers();

// Blazor
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddBootstrapBlazor();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AspBaseProj.Presentation.Components.Shared.ApiClient>();
builder.Services.AddScoped<AspBaseProj.Presentation.Components.Shared.CascadingAuthState>();

// Localization — registered AFTER AddBootstrapBlazor() to override its JsonStringLocalizer
// with the standard .NET ResourceManagerStringLocalizer that reads .resx files.
// No ResourcesPath needed — the SharedResource type is in namespace AspBaseProj.Presentation.Resources
// which maps to the Resources/ folder by default convention.
builder.Services.AddLocalization();

// Override BootstrapBlazor's JsonStringLocalizer with the standard .resx-based localizer
builder.Services.AddSingleton<IStringLocalizerFactory, ResourceManagerStringLocalizerFactory>();
builder.Services.AddTransient(typeof(IStringLocalizer<>), typeof(StringLocalizer<>));

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("en"),
        new CultureInfo("de"),
    };
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

// OpenAPI / Swagger
builder.Services.AddOpenApi();

// Authentication
var jwtKey = builder.Configuration["Jwt:Key"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = 401;
            return Task.CompletedTask;
        }
    };
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        RoleClaimType = "groups",
        NameClaimType = System.Security.Claims.ClaimTypes.Name
    };
});

// OAuth Providers (only if configured)
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
    builder.Services.AddAuthentication().AddGoogle(o =>
    {
        o.ClientId = googleClientId;
        o.ClientSecret = googleClientSecret;
        o.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    });

var githubClientId = builder.Configuration["Authentication:GitHub:ClientId"];
var githubClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"];
if (!string.IsNullOrEmpty(githubClientId) && !string.IsNullOrEmpty(githubClientSecret))
    builder.Services.AddAuthentication().AddGitHub(o =>
    {
        o.ClientId = githubClientId;
        o.ClientSecret = githubClientSecret;
        o.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    });

var msClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
var msClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];
if (!string.IsNullOrEmpty(msClientId) && !string.IsNullOrEmpty(msClientSecret))
    builder.Services.AddAuthentication().AddMicrosoftAccount(o =>
    {
        o.ClientId = msClientId;
        o.ClientSecret = msClientSecret;
        o.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    });

// Authorization Policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RootPolicy", p => p.RequireClaim("is_root", "true"))
    .AddPolicy("AdminPolicy", p => p.RequireClaim("groups", "Admin"))
    .AddPolicy("AuthorPolicy", p => p.RequireClaim("groups", "Author"))
    .AddPolicy("AdminOrRootPolicy", p =>
        p.RequireAssertion(ctx =>
            ctx.User.HasClaim("is_root", "true") ||
            ctx.User.HasClaim("groups", "Admin")))
    .AddPolicy("AuthorOrAdminOrRootPolicy", p =>
        p.RequireAssertion(ctx =>
            ctx.User.HasClaim("is_root", "true") ||
            ctx.User.HasClaim("groups", "Admin") ||
            ctx.User.HasClaim("groups", "Author")));

var app = builder.Build();

// Auto-migrate and seed
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DatabaseSeeder.SeedAsync(app.Services, builder.Configuration, logger);
}

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();
app.UseRequestLocalization();
app.UseAntiforgery();

// Swagger
app.MapOpenApi();

// Controllers
app.MapControllers();

// Blazor
app.MapRazorComponents<AspBaseProj.Presentation.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
