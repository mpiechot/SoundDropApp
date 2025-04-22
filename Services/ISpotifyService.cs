

using SoundDrop.Models;

namespace SoundDrop.Services
{
    public interface ISpotifyService
    {
        Task AddTrackToQueueAsync(string trackUri);
        Task AuthenticateAsync();
        Task<IEnumerable<TrackModel>> SearchTracksAsync(string query);

        void SetAuthenticationCode(string code);

        bool IsLoggedIn { get; }

        bool HasAccessToken { get; }
    }
}
