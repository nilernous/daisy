using Daisy.Application.Interfaces;
using Daisy.Infrastructure.Authentication;
using Daisy.Infrastructure.BackgroundJobs;
using Daisy.Infrastructure.Email;
using Daisy.Infrastructure.Payments;
using Daisy.Infrastructure.Persistence;
using Daisy.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Daisy.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Daisy")
            ?? throw new InvalidOperationException("Connection string 'Daisy' is not configured.");

        services.AddDbContext<DaisyDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<DaisyDbContext>());
        services.AddSingleton<IDatabaseErrorClassifier, SqlServerErrorClassifier>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<IStorageService, LocalStorageService>();

        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        if (string.IsNullOrWhiteSpace(configuration[$"{SmtpOptions.SectionName}:Host"]))
        {
            services.AddSingleton<IEmailService, LoggingEmailService>();
        }
        else
        {
            services.AddSingleton<IEmailService, SmtpEmailService>();
        }

        services.Configure<VietQrOptions>(configuration.GetSection(VietQrOptions.SectionName));
        var vietQr = configuration.GetSection(VietQrOptions.SectionName).Get<VietQrOptions>() ?? new VietQrOptions();
        if (vietQr.IsMock)
        {
            services.AddSingleton<MockPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<MockPaymentGateway>());
            services.AddSingleton<IPaymentSimulator>(sp => sp.GetRequiredService<MockPaymentGateway>());
        }
        else if (string.Equals(vietQr.Provider, "SePay", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IPaymentGateway, SePayPaymentGateway>();
        }
        else
        {
            throw new InvalidOperationException($"Unknown VietQr:Provider '{vietQr.Provider}'. Use SePay or Mock.");
        }

        if (configuration.GetValue("BackgroundJobs:Enabled", true))
        {
            services.AddHostedService<UnpaidOrderExpiryJob>();
        }

        return services;
    }
}
