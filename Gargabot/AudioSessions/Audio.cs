namespace Gargabot.AudioSessions
{
    public class Audio
    {
        private string url, title;
        public Audio(string url, string title)
        {
            this.url = url;
            this.title = title;
        }

        public string Url { get => url; set => url = value; }
        public string Title { get => title; set => title = value; }
    }
}
