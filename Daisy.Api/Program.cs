using System.Threading.RateLimiting;
using Daisy.Api.Infrastructure;
using Daisy.Api.Middleware;
using Daisy.Api.OData;
using Daisy.Application;
using Daisy.Application.Common;
using Daisy.Application.Interfaces;
using Daisy.Domain.Common;
using Daisy.Infrastructure;
using Daisy.Infrastructure.Authentication;
using Daisy.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Query.Expressions;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OData.UriParser;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

services.AddApplication();
services.AddInfrastructure(configuration);

services.Configure<CheckoutOptions>(configuration.GetSection(CheckoutOptions.SectionName));
services.Configure<PaymentDeadlineOptions>(configuration.GetSection(PaymentDeadlineOptions.SectionName));
services.Configure<PasswordResetOptions>(configuration.GetSection(PasswordResetOptions.SectionName));
services.Configure<UploadOptions>(configuration.GetSection(UploadOptions.SectionName));

services.AddHttpContextAccessor();
services.AddScoped<ICurrentUserService, CurrentUserService>();

// ---- Controllers, JSON and OData (API_DESIGN §2, §7) -----------------------------------
services
    .AddControllers()
    .AddJsonOptions(o => ApiJson.Configure(o.JsonSerializerOptions))
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ErrorResponse.FromModelState)
    .AddOData(o =>
    {
        o.TimeZone = VietnamTime.TimeZone;
        o.RouteOptions.EnableControllerNameCaseInsensitive = true;
        o.Select().Filter().OrderBy().Count().SetMaxTop(100);
        o.AddRouteComponents("odata", EdmModelBuilder.Build(), routeServices =>
            routeServices.AddSingleton<ODataUriResolver>(new StringAsEnumResolver { EnableCaseInsensitive = true }));
    });

// ---- Authentication and authorization (API_DESIGN §3) -------------------------------------
var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = JwtTokenService.CreateSigningKey(jwt),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role",
        };
        o.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                return ErrorResponse.WriteAsync(context.HttpContext, 401, ErrorCodes.Unauthorized, "Sign in to continue.");
            },
            OnForbidden = context =>
                ErrorResponse.WriteAsync(context.HttpContext, 403, ErrorCodes.Forbidden, "You do not have permission to do this."),
        };
    });

services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Authenticated, p => p.RequireAuthenticatedUser())
    .AddPolicy(Policies.Customer, p => p.RequireRole("Customer"))
    .AddPolicy(Policies.Staff, p => p.RequireRole("Staff", "Admin"))
    .AddPolicy(Policies.Admin, p => p.RequireRole("Admin"));

// ---- Rate limiting (API_DESIGN §10) -------------------------------------------------------
services.AddRateLimiter(o =>
{
    static RateLimitPartition<string> PerIp(HttpContext context, int permits, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = window, QueueLimit = 0 });

    var limits = configuration.GetSection("RateLimits");
    o.AddPolicy(RateLimits.Auth, c => PerIp(c, limits.GetValue("Auth", 10), TimeSpan.FromMinutes(1)));
    o.AddPolicy(RateLimits.Password, c => PerIp(c, limits.GetValue("Password", 5), TimeSpan.FromMinutes(15)));
    o.AddPolicy(RateLimits.Voucher, c => PerIp(c, limits.GetValue("Voucher", 20), TimeSpan.FromMinutes(1)));
    o.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        }

        return new ValueTask(ErrorResponse.WriteAsync(
            context.HttpContext, 429, ErrorCodes.TooManyRequests, "Too many requests. Please wait and try again."));
    };
});

// ---- Swagger (Development only) -------------------------------------------------------------
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Daisy API", Version = "v1" });
    o.DocInclusionPredicate((_, api) => api.RelativePath is not null && !api.RelativePath.StartsWith("odata/$", StringComparison.Ordinal));
    o.CustomSchemaIds(t => t.FullName!.Replace("+", "."));
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
    });
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseODataRouteDebug();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// Uploaded images stored by LocalStorageService, served under /uploads.
var storage = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
var storageRoot = LocalStorageService.ResolveRoot(storage, app.Environment);
Directory.CreateDirectory(storageRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storageRoot),
    RequestPath = "/" + storage.PublicBasePath.Trim('/'),
    ServeUnknownFileTypes = false,
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>Entry point, public for WebApplicationFactory in Daisy.Tests.</summary>
public partial class Program;
