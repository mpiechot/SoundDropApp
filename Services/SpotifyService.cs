using SoundDrop.Models;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SoundDrop.Services;

public class SpotifyService : ISpotifyService
{
    private readonly HttpClient http;
    private readonly IConfiguration config;

    private string? accessToken;
    private string? authCode;
    private DateTime tokenExpiry;

    public SpotifyService(HttpClient http, IConfiguration config)
    {
        this.http = http;
        this.config = config;
    }

    public async Task AuthenticateAsync()
    {
        var clientId = config["Spotify:ClientId"];
        var clientSecret = config["Spotify:ClientSecret"];

        if (clientId == null || clientSecret == null)
        {
            Console.Error.WriteLine("No ClientID or ClientSecret provided");
            return;
        }

        var authHeader = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

        if (authCode == null)
        {
            Console.Error.WriteLine("Authentication Token not set.");
            return;
        }

        var redirectUri = "https://127.0.0.1:7041/";

        var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"grant_type", "authorization_code"},
                {"code", authCode},
                {"redirect_uri", redirectUri}
            })
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        accessToken = doc.RootElement.GetProperty("access_token").GetString();
        var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
        tokenExpiry = DateTime.Now.AddSeconds(expiresIn - 60);

        // Optional: Refresh Token speichern für spätere Verwendung
        if (doc.RootElement.TryGetProperty("refresh_token", out var refreshTokenElement))
        {
            var refreshToken = refreshTokenElement.GetString();
            // Save refreshToken somewhere!
        }
    }

    public void SetAuthenticationCode(string code)
    {
        authCode = code;
    }

    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(authCode);
    public bool HasAccessToken => !string.IsNullOrWhiteSpace(accessToken);

    public async Task<IEnumerable<TrackModel>> SearchTracksAsync(string query)
    {
        if (!HasAccessToken || tokenExpiry <= DateTime.Now)
            await AuthenticateAsync();

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.spotify.com/v1/search?q={Uri.EscapeDataString(query)}&type=track&limit=10"
        );

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        var songList = new List<TrackModel>();

        var items = doc.RootElement
                       .GetProperty("tracks")
                       .GetProperty("items")
                       .EnumerateArray();

        foreach (var item in items)
        {
            var song = new TrackModel
            {
                Name = item.GetProperty("name").GetString() ?? string.Empty,
                Artist = item.GetProperty("artists")[0].GetProperty("name").GetString() ?? string.Empty,
                Uri = item.GetProperty("uri").GetString() ?? string.Empty
            };

            var images = item.GetProperty("album").GetProperty("images");
            if (images.GetArrayLength() > 2)
                song.ThumbnailUrl = images[2].GetProperty("url").GetString() ?? string.Empty; // kleines Bild
            else if (images.GetArrayLength() > 0)
                song.ThumbnailUrl = images[0].GetProperty("url").GetString() ?? string.Empty;

            songList.Add(song);
        }

        return songList;
    }

    public async Task AddTrackToQueueAsync(string trackUri)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.spotify.com/v1/me/player/queue?uri={Uri.EscapeDataString(trackUri)}"
        );

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await http.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Fehler beim Hinzufügen zur Warteschlange: {error}");
        }
    }
}
