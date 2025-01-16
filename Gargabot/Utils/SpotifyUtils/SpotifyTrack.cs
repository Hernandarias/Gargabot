using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gargabot.Utils.SpotifyUtils
{
    public class SpotifyTrack
    {
        private string title;
        private string fullTitle;
        private string url;

        public SpotifyTrack(string title, string fullTitle, string url)
        {
            this.title = title;
            this.fullTitle = fullTitle;
            this.url = url;
        }

        public string Title
        {
            get { return title; }
            set { title = value; }
        }

        public string FullTitle
        {
            get { return fullTitle; }
            set { fullTitle = value; }
        }

        public string Url
        {
            get { return url; }
            set { url = value; }
        }

    }
}
