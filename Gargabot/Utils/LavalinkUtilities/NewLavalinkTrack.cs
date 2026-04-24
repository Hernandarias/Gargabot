using Lavalink4NET.Tracks;

namespace Gargabot.Utils.LavalinkUtilities
{
    public class NewLavalinkTrack
    {
        public string FinalTitle { get; set; } = "";
        public string FullTitle { get; set; } = "";
        public string Url { get; set; } = "";
        public string PlayableIdentifier { get; set; } = "";
        public string ThumbnailUrl { get; set; } = "";
        public string Duration { get; set; } = "";
        public string SpotifyTrackId { get; set; } = "";
        public string YoutubeVideoId { get; set; } = "";
        public List<string> SpotifyArtistsIds { get; set; } = new();
        public string DeezerTrackId { get; set; } = "";
        public LavalinkTrack? Track { get; set; }

        public NewLavalinkTrack() { }

        public NewLavalinkTrack(string finalTitle, string url, string thumbnailUrl, string duration, LavalinkTrack? track = null)
        {
            FinalTitle = finalTitle;
            Url = url;
            ThumbnailUrl = thumbnailUrl;
            Duration = duration;
            Track = track;
        }

        public NewLavalinkTrack(string finalTitle, string url, string playableIdentifier, string thumbnailUrl, string duration, LavalinkTrack? track = null)
            : this(finalTitle, url, thumbnailUrl, duration, track)
        {
            PlayableIdentifier = playableIdentifier;
        }

        public NewLavalinkTrack(NewLavalinkTrack track)
        {
            FinalTitle = track.FinalTitle;
            FullTitle = track.FullTitle;
            Url = track.Url;
            PlayableIdentifier = track.PlayableIdentifier;
            ThumbnailUrl = track.ThumbnailUrl;
            Duration = track.Duration;
            SpotifyTrackId = track.SpotifyTrackId;
            YoutubeVideoId = track.YoutubeVideoId;
            SpotifyArtistsIds = new List<string>(track.SpotifyArtistsIds);
            DeezerTrackId = track.DeezerTrackId;
            Track = track.Track;
        }
    }
}