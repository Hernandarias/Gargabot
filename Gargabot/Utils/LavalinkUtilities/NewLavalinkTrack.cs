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
        private string url;
        private string thumbnailUrl;
        private LavalinkTrack track;
        private string duration;
        private LavalinkNodeConnection node;
        public NewLavalinkTrack(string finalTitle, string url, string thumbnailUrl, string duration, LavalinkTrack track, LavalinkNodeConnection node)
        {
            this.finalTitle = finalTitle;
            this.url = url;
            this.track = track;
            this.thumbnailUrl = thumbnailUrl;
            this.duration = duration;
            this.node = node;
        }
        public string FinalTitle { get => finalTitle; set => finalTitle = value; }
        public string Url { get => url; set => url = value; }
        public LavalinkTrack Track { get => track; set => track = value; }
        public string ThumbnailUrl { get => thumbnailUrl; set => thumbnailUrl = value; }
        public string Duration { get => duration; set => duration = value; }
        public LavalinkNodeConnection Node { get => node; set => node = value; }
    }
}
