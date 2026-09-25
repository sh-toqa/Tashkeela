using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tashkeela.Application.Common;
using Tashkeela.Infrastructure.Email;
using Tashkeela.Infrastructure.Persistence;

namespace Tashkeela.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Resolved when the first DbContext is created, so hosts (and test factories) can supply it late.
        services.AddDbContext<AppDbContext>((sp, options) => options.UseSqlServer(
            sp.GetRequiredService<IConfiguration>().GetConnectionString("Tashkeela")
            ?? throw new InvalidOperationException("Set ConnectionStrings:Tashkeela (User Secrets in development).")));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>()); // same instance per request

        services.AddSingleton<IEmailSender, LoggingEmailSender>();
        return services;
    }
}
