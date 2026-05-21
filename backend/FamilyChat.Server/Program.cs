using FamilyChat.Server.Domain;
using FamilyChat.Server.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace FamilyChat.Server
{
    public class Program
    {
        private static readonly ConcurrentDictionary<string, string> GoogleLoginCodes = new();

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddDbContext<ApplicationIdentityDbContext>(
                options => options.UseNpgsql(builder.Configuration.GetConnectionString("postgresql")));

            builder.Services.AddIdentityApiEndpoints<User>(options =>
            {
                options.User.RequireUniqueEmail = true;
            })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationIdentityDbContext>()
                .AddDefaultTokenProviders();

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = IdentityConstants.BearerScheme;
                options.DefaultChallengeScheme = IdentityConstants.BearerScheme;
            })
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
                var externalLoginInfo = await signInManager.GetExternalLoginInfoAsync();

                if (externalLoginInfo is null)
                {
                    return CreateGoogleLoginRedirectResult(false, "Google login information was not available.", null, returnUrl);
                }

                var user = await userManager.FindByLoginAsync(
                    externalLoginInfo.LoginProvider,
                    externalLoginInfo.ProviderKey);

                if (user is null)
                {
                    var email = externalLoginInfo.Principal.FindFirstValue(ClaimTypes.Email) ??
                        externalLoginInfo.Principal.FindFirstValue("email");

                    if (string.IsNullOrWhiteSpace(email))
                    {
                        return CreateGoogleLoginRedirectResult(false, "Google did not provide an email address.", null, returnUrl);
                    }

                    user = await userManager.FindByEmailAsync(email);

                    if (user is null)
                    {
                        user = new User
                        {
                            UserName = externalLoginInfo.Principal.FindFirstValue("name")?.Replace(" ","") ?? email,
                            Email = email,
                            EmailConfirmed = true,
                        };

                        var createResult = await userManager.CreateAsync(user);

                        if (!createResult.Succeeded)
                        {
                            return CreateGoogleLoginRedirectResult(
                                false,
                                createResult.Errors.FirstOrDefault()?.Description ?? "Could not create the user.",
                                null,
                                returnUrl);
                        }
                    }

                    var addLoginResult = await userManager.AddLoginAsync(user, externalLoginInfo);

                    if (!addLoginResult.Succeeded)
                    {
                        if (addLoginResult.Errors.Any(error => error.Code == "LoginAlreadyAssociated"))
                        {
                            user = await userManager.FindByLoginAsync(
                                externalLoginInfo.LoginProvider,
                                externalLoginInfo.ProviderKey);

                            if (user is not null)
                            {
                                return CreateGoogleLoginRedirectResult(true, null, CreateGoogleLoginCode(user), returnUrl);
                            }
                        }

                        return CreateGoogleLoginRedirectResult(
                            false,
                            addLoginResult.Errors.FirstOrDefault()?.Description ?? "Could not link the Google login.",
                            null,
                            returnUrl);
                    }
                }

                return CreateGoogleLoginRedirectResult(true, null, CreateGoogleLoginCode(user), returnUrl);
            });

            app.MapPost("/auth/login/google/token", async (
                GoogleLoginTokenRequest request,
                UserManager<User> userManager,
                SignInManager<User> signInManager) =>
            {
                if (string.IsNullOrWhiteSpace(request.Code) ||
                    !GoogleLoginCodes.TryRemove(request.Code, out var userId))
                {
                    return Results.BadRequest("Google login code is invalid or expired.");
                }

                var user = await userManager.FindByIdAsync(userId);

                if (user is null)
                {
                    return Results.BadRequest("Google login user was not found.");
                }

                return await CreateBearerTokenResult(user, signInManager);
            });

            app.MapPost("/auth/logout", () => Results.NoContent());

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

        private static async Task<IResult> CreateBearerTokenResult(User user, SignInManager<User> signInManager)
        {
            var principal = await signInManager.CreateUserPrincipalAsync(user);
            return Results.SignIn(principal, authenticationScheme: IdentityConstants.BearerScheme);
        }

        private static string CreateGoogleLoginCode(User user)
        {
            var code = Guid.NewGuid().ToString("N");
            GoogleLoginCodes[code] = user.Id;
            return code;
        }

        private static IResult CreateGoogleLoginRedirectResult(bool succeeded, string? error, string? code, string? returnUrl)
        {
            var safeReturnUrl = NormalizeReturnUrl(returnUrl);
            var redirectUrl = succeeded && !string.IsNullOrWhiteSpace(code)
                ? AppendQueryParameter(safeReturnUrl, "googleLoginCode", code)
                : AppendQueryParameter(safeReturnUrl, "googleLoginError", error ?? "Google login failed.");

            return Results.LocalRedirect(redirectUrl);
        }

        private static string AppendQueryParameter(string url, string name, string value)
        {
            var fragmentIndex = url.IndexOf('#');
            var fragment = fragmentIndex >= 0 ? url[fragmentIndex..] : string.Empty;
            var pathAndQuery = fragmentIndex >= 0 ? url[..fragmentIndex] : url;
            var separator = pathAndQuery.Contains('?') ? "&" : "?";

            return $"{pathAndQuery}{separator}{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}{fragment}";
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

        private sealed record GoogleLoginTokenRequest(string Code);
    }
}
