using Budgie.Modules.Users.Application.Register;
using Budgie.Modules.Users.Infrastructure;
using Budgie.Modules.Users.Infrastructure.Authentication;
using Budgie.Modules.Users.Infrastructure.Database;
using Budgie.Shared.Abstractions;
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
        services.AddScoped<Application.IRegistrationPolicy, RegistrationPolicy>();
        services.AddScoped<Application.IUserRepository, UserRepository>();
        services.AddScoped<Application.IUserSettingsRepository, UserSettingsRepository>();
        services.AddScoped<Application.IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<Application.IUserInviteRepository, UserInviteRepository>();
        return services;
    }

    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users");

        group.MapGet("/registration-status", async (Application.IRegistrationPolicy policy, CancellationToken ct) =>
        {
            var status = await policy.GetStatusAsync(ct);
            return Results.Ok(status);
        })
        .Produces<RegistrationStatusResponse>(200);

        group.MapPost("/register", async (Application.Register.RegisterUserCommand command, Mediator.IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.Match(
                userId => Results.Created($"/api/users/{userId}", new { Id = userId }),
                errors => result.FirstError.Type == ErrorOr.ErrorType.Forbidden
                    ? Results.Json(errors, statusCode: StatusCodes.Status403Forbidden)
                    : Results.BadRequest(errors));
        })
        .Produces<Budgie.Shared.Dtos.CreatedResponse>(StatusCodes.Status201Created);

        group.MapPost("/login", async (Application.Login.LoginCommand command, Mediator.IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.Match(
                response => Results.Ok(response),
                errors => Results.Unauthorized());
        })
        .Produces<Application.Login.LoginResponse>(200);

        group.MapPost("/refresh", async (Application.Refresh.RefreshCommand command, Mediator.IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.Match(
                response => Results.Ok(response),
                errors => Results.Unauthorized());
        })
        .Produces<Application.Login.LoginResponse>(200);

        group.MapPost("/invites", async (Application.Invites.CreateInviteCommand command, Mediator.IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.Match(
                response => Results.Ok(response),
                errors => result.FirstError.Type == ErrorOr.ErrorType.Forbidden
                    ? Results.Forbid()
                    : Results.BadRequest(errors));
        })
        .RequireAuthorization()
        .Produces<Application.Invites.CreateInviteResponse>(StatusCodes.Status200OK);

        group.MapPost("/admin/create-user", async (Application.Admin.CreateUser.AdminCreateUserCommand command, Mediator.IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.Match(
                userId => Results.Created($"/api/users/{userId}", new { Id = userId }),
                errors => result.FirstError.Type == ErrorOr.ErrorType.Forbidden
                    ? Results.Forbid()
                    : Results.BadRequest(errors));
        })
        .RequireAuthorization()
        .Produces<Budgie.Shared.Dtos.CreatedResponse>(StatusCodes.Status201Created);

        Budgie.Modules.Users.Presentation.Settings.SettingsEndpoints.MapEndpoints(group);
        Budgie.Modules.Users.Presentation.Profile.ProfileEndpoints.MapEndpoints(group);

        return app;
    }
}
