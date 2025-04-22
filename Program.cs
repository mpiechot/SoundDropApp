using SoundDrop.Components;
using SoundDrop.Services;

namespace SoundDrop
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddSingleton<ISpotifyService, SpotifyService>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();

                var httpClient = new HttpClient();
                return new SpotifyService(httpClient, config);
            });

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.MapGet("/callback/", async (HttpContext context, IConfiguration config) =>
            {
                var code = context.Request.Query["code"];
                if (string.IsNullOrEmpty(code))
                    return Results.BadRequest("Kein Code von Spotify erhalten!");

                var clientId = config["Spotify:ClientId"];
                var clientSecret = config["Spotify:ClientSecret"];
                var redirectUri = "https://localhost:5001/callback/";

                using var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
                request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "code", code },
                    { "redirect_uri", redirectUri },
                    { "client_id", clientId },
                    { "client_secret", clientSecret }
                });

                var response = await client.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                // TODO: Token speichern
                return Results.Ok($"Token-Response: {json}");
            });


            app.UseHttpsRedirection();

            app.UseStaticFiles();
            app.UseAntiforgery();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}
