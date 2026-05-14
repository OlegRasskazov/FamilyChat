using FamilyChat.Server.Domain;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace FamilyChat.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddDbContext<ApplicationIdentityDbContext>(
                options => options.UseNpgsql(builder.Configuration.GetConnectionString("postgresql")));

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = "GoogleOpenIdConnect";
            })
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddGoogleOpenIdConnect(googleOptions =>
                {
                    googleOptions.Authority = "https://accounts.google.com";
                    googleOptions.ClientId = builder.Configuration["Authentication:Google:ClientId"];
                    googleOptions.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
                    googleOptions.ResponseType = "code";
                    googleOptions.CallbackPath = "/signin-google";

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

            app.MapControllers();

            app.MapFallbackToFile("/index.html");

            app.Run();
        }
    }
}
