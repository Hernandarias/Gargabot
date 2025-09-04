using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gargabot.Utils.DeezerUtils
{
    public class DeezerTrack
    {
        private string title;
        private string fullTitle;
        private string url;
        private string isrc;

        public DeezerTrack()
        {
            title = "";
            fullTitle = "";
            url = "";
            isrc = "";
        }

        public DeezerTrack(string title, string fullTitle, string url, string isrc)
        {
            this.title = title;
            this.fullTitle = fullTitle;
            this.url = url;
            this.isrc = isrc;
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

        public string Isrc
        {
            get { return isrc; }
            set { isrc = value; }
        }
    }
}
