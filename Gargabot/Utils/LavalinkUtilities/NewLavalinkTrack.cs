using DSharpPlus.Lavalink;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gargabot.Utils.LavalinkUtilities
{
    public class NewLavalinkTrack
    {
        private string finalTitle;
        private string fullTitle;
        private string url;
        private string thumbnailUrl;
        private LavalinkTrack track;
        private string duration;
        private LavalinkNodeConnection node;
        private string spotifyTrackId;
        private string youtubeVideoId;
        private List<string> spotifyArtistsIds;
        private string deezerTrackId;
        public NewLavalinkTrack(string finalTitle, string url, string thumbnailUrl, string duration, LavalinkTrack track, LavalinkNodeConnection node)
        {
            this.finalTitle = finalTitle;
            this.url = url;
            this.track = track;
            this.thumbnailUrl = thumbnailUrl;
            this.duration = duration;
            this.node = node;
        }

        public NewLavalinkTrack(NewLavalinkTrack track)
        {
            this.finalTitle = track.FinalTitle;
            this.url = track.Url;
            this.track = track.Track;
            this.thumbnailUrl = track.ThumbnailUrl;
            this.duration = track.Duration;
            this.node = track.Node;
            this.fullTitle = track.FullTitle;
            this.spotifyTrackId = track.SpotifyTrackId;
            this.spotifyArtistsIds = track.SpotifyArtistsIds;
            this.youtubeVideoId = track.YoutubeVideoId;
            this.deezerTrackId = track.DeezerTrackId;
        }

        public NewLavalinkTrack() { }
        public string FinalTitle { get => finalTitle; set => finalTitle = value; }

        public string YoutubeVideoId { get => youtubeVideoId; set => youtubeVideoId = value; }
        public string Url { get => url; set => url = value; }
        public LavalinkTrack Track { get => track; set => track = value; }
        public string ThumbnailUrl { get => thumbnailUrl; set => thumbnailUrl = value; }
        public string Duration { get => duration; set => duration = value; }
        public LavalinkNodeConnection Node { get => node; set => node = value; }
        public string FullTitle { get => fullTitle; set => fullTitle = value; }
        public string SpotifyTrackId { get => spotifyTrackId; set => spotifyTrackId = value; }
        public List<string> SpotifyArtistsIds { get => spotifyArtistsIds; set => spotifyArtistsIds = value; }

        public string DeezerTrackId { get => deezerTrackId; set => deezerTrackId = value; }
    }
}
