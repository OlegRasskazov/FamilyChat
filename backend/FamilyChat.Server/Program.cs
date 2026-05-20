using FamilyChat.Server.Domain;
using FamilyChat.Server.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FamilyChat.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddDbContext<ApplicationIdentityDbContext>(
                options => options.UseNpgsql(builder.Configuration.GetConnectionString("postgresql")));

            builder.Services.AddIdentity<User, IdentityRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
            })
                .AddEntityFrameworkStores<ApplicationIdentityDbContext>()
                .AddDefaultTokenProviders();

            builder.Services.AddAuthentication()
                .AddGoogleOpenIdConnect(googleOptions =>
                {
                    googleOptions.Authority = "https://accounts.google.com";
                    googleOptions.ClientId = builder.Configuration["Authentication:Google:ClientId"];
                    googleOptions.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
                    googleOptions.ResponseType = "code";
                    googleOptions.CallbackPath = "/signin-google";
                    googleOptions.SignInScheme = IdentityConstants.ExternalScheme;

                    googleOptions.SaveTokens = true;

                    googleOptions.Scope.Clear();
                    googleOptions.Scope.Add("openid");
                    googleOptions.Scope.Add("profile");
                    googleOptions.Scope.Add("email");
                });

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();


            var app = builder.Build();

            app.UseDefaultFiles();
            app.MapStaticAssets();


            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();

                using (var scope = app.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationIdentityDbContext>();
                    db.Database.Migrate();
                }
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapGet("/auth/login/google", (string? returnUrl, SignInManager<User> signInManager) =>
            {
                var redirectUrl = $"/auth/login/google/callback?returnUrl={Uri.EscapeDataString(NormalizeReturnUrl(returnUrl))}";
                var properties = signInManager.ConfigureExternalAuthenticationProperties("GoogleOpenIdConnect", redirectUrl);

                return Results.Challenge(properties, ["GoogleOpenIdConnect"]);
            });

            app.MapGet("/auth/login/google/callback", async (
                string? returnUrl,
                UserManager<User> userManager,
                SignInManager<User> signInManager) =>
            {
                var safeReturnUrl = NormalizeReturnUrl(returnUrl);
                var externalLoginInfo = await signInManager.GetExternalLoginInfoAsync();

                if (externalLoginInfo is null)
                {
                    return Results.Redirect($"/auth/login/google?returnUrl={Uri.EscapeDataString(safeReturnUrl)}");
                }

                var signInResult = await signInManager.ExternalLoginSignInAsync(
                    externalLoginInfo.LoginProvider,
                    externalLoginInfo.ProviderKey,
                    isPersistent: false,
                    bypassTwoFactor: true);

                if (signInResult.Succeeded)
                {
                    await signInManager.UpdateExternalAuthenticationTokensAsync(externalLoginInfo);
                    return Results.LocalRedirect(safeReturnUrl);
                }

                var email = externalLoginInfo.Principal.FindFirstValue(ClaimTypes.Email) ??
                    externalLoginInfo.Principal.FindFirstValue("email");

                if (string.IsNullOrWhiteSpace(email))
                {
                    return Results.BadRequest("Google did not provide an email address.");
                }

                var user = await userManager.FindByEmailAsync(email);

                if (user is null)
                {
                    user = new User
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true
                    };

                    var createResult = await userManager.CreateAsync(user);

                    if (!createResult.Succeeded)
                    {
                        return Results.ValidationProblem(createResult.Errors.ToDictionary(
                            error => error.Code,
                            error => new[] { error.Description }));
                    }
                }

                var addLoginResult = await userManager.AddLoginAsync(user, externalLoginInfo);

                if (!addLoginResult.Succeeded)
                {
                    if (addLoginResult.Errors.Any(error => error.Code == "LoginAlreadyAssociated"))
                    {
                        var linkedUser = await userManager.FindByLoginAsync(
                            externalLoginInfo.LoginProvider,
                            externalLoginInfo.ProviderKey);

                        if (linkedUser is not null)
                        {
                            await signInManager.SignInAsync(linkedUser, isPersistent: false);
                            return Results.LocalRedirect(safeReturnUrl);
                        }
                    }

                    return Results.ValidationProblem(addLoginResult.Errors.ToDictionary(
                        error => error.Code,
                        error => new[] { error.Description }));
                }

                await signInManager.SignInAsync(user, isPersistent: false);
                await signInManager.UpdateExternalAuthenticationTokensAsync(externalLoginInfo);

                return Results.LocalRedirect(safeReturnUrl);
            });

            app.MapGet("/auth/logout", (string? returnUrl) =>
                Results.SignOut(
                    new AuthenticationProperties
                    {
                        RedirectUri = NormalizeReturnUrl(returnUrl)
                    },
                    [IdentityConstants.ApplicationScheme]));

            app.MapGet("/auth/me", async (ClaimsPrincipal principal, UserManager<User> userManager) =>
            {
                if (principal.Identity?.IsAuthenticated != true)
                {
                    return Results.Ok(new { isAuthenticated = false });
                }

                var user = await userManager.GetUserAsync(principal);

                return Results.Ok(new
                {
                    isAuthenticated = true,
                    id = user?.Id,
                    name = principal.FindFirstValue(ClaimTypes.Name) ?? principal.FindFirstValue("name"),
                    email = user?.Email ?? principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue("email"),
                    pictureUrl = principal.FindFirstValue("picture")
                });
            });

            app.MapControllers();

            app.MapFallbackToFile("/index.html");

            app.Run();
        }

        private static string NormalizeReturnUrl(string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl) ||
                !Uri.IsWellFormedUriString(returnUrl, UriKind.Relative) ||
                !returnUrl.StartsWith('/') ||
                returnUrl.StartsWith("//"))
            {
                return "/";
            }

            return returnUrl;
        }
    }
}
