using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gargabot.Utils.Youtube
{
    public class YoutubeVideo
    {
        private string title;
        private string url;
        private string thumbnail;
        private string duration;
        private string author;
        private string id;
        private string views;

        public YoutubeVideo(string title, string url, string thumbnail, string duration, string author, string views, string id)
        {
            this.title = title;
            this.url = url;
            this.thumbnail = thumbnail;
            this.duration = duration;
            this.author = author;
            this.views = views;
            this.id = id;
        }
        public string Title { get => title; set => title = value; }
        public string Url { get => url; set => url = value; }
        public string Thumbnail { get => thumbnail; set => thumbnail = value; }
        public string Duration {    get => duration; set => duration = value; }
        public string Author { get => author; set => author = value; }
        public string Views { get => views; set => views = value;}

        public string Id { get => id; set => id = value; }

    }
}
