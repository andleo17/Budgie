using Budgie.Modules.Users.Application.Login;
using Budgie.Modules.Users.Application.Refresh;
using Budgie.Modules.Users.Application.Register;
using Budgie.Modules.Users.Infrastructure;
using Budgie.Modules.Users.Infrastructure.Authentication;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Dtos;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Budgie.Modules.Users;

public static class UsersModule
{
    public static IServiceCollection AddUsersModule(this IServiceCollection services)
    {
        services.AddScoped<IJwtProvider, JwtProvider>();
        services.AddScoped<IUserSettingsProvider, UserSettingsProvider>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        return services;
    }

    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users");

        group.MapPost("/register", async (RegisterUserCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.Match(
                userId => Results.Created($"/api/users/{userId}", new { Id = userId }),
                errors => Results.BadRequest(errors));
        })
        .Produces<CreatedResponse>(StatusCodes.Status201Created);

        group.MapPost("/login", async (LoginCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.Match(
                response => Results.Ok(response),
                errors => Results.Unauthorized());
        })
        .Produces<LoginResponse>(200);

        group.MapPost("/refresh", async (RefreshCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.Match(
                response => Results.Ok(response),
                errors => Results.Unauthorized());
        })
        .Produces<LoginResponse>(200);

        Budgie.Modules.Users.Presentation.Settings.SettingsEndpoints.MapEndpoints(group);
        Budgie.Modules.Users.Presentation.Profile.ProfileEndpoints.MapEndpoints(group);

        return app;
    }
}
