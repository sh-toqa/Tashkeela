using Microsoft.Extensions.DependencyInjection;
using Tashkeela.Application.Events;
using Tashkeela.Application.Teams;
using Tashkeela.Application.Users;

namespace Tashkeela.Application;

public static class DependencyInjection
{
    /// <summary>One line per service, so "what can the application do?" is answered here.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services) => services
        .AddScoped<TeamService>()
        .AddScoped<InvitationService>()
        .AddScoped<EventService>()
        .AddScoped<RsvpService>()
        .AddScoped<UserService>();
}
