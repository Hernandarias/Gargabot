using DeezNET;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Gargabot.Utils.DeezerUtils
{
    public static class DeezerController
    {
        public static async Task<DeezerTrack> GetTrackFromQueryAsync(string query)
        {
            DeezerTrack dt = new DeezerTrack();
            try
            {
                query = SanitizeTitle(query);

                var client = new DeezerClient();
                var track = await client.PublicApi.SearchTrack(query, limit: 1);
                var trackData = track["data"]!.First();
                trackData = await client.PublicApi.GetTrack((long)trackData["id"]!);
                string artists = "";
                for (int x = 0; x < trackData["contributors"]!.Count(); x++)
                {
                    artists += trackData["contributors"]![x]!["name"]!;
                    if (x != trackData["contributors"]!.Count() - 1)
                    {
                        artists += ", ";
                    }
                }
                dt.Title = trackData["title"]!.ToString();
                dt.FullTitle = $"{trackData["title"]!} ¡] {artists} ({trackData["album"]!["title"]})";
                dt.Url = BuildTrackURL((long)trackData["id"]!);
                dt.Isrc = trackData["isrc"]!.ToString();
            }
            catch (Exception ex) { }
            return dt;
        }

        public static async Task<Tuple<string,string>> GetTrackDescription(string url)
        {
            Tuple<string, string> trackDescription = new Tuple<string, string>("", ""); //ISRC, Name
            try
            {
                var client = new DeezerClient();
                var trackId = await GetTrackIdFromUrlAsync(url);
                var track = await client.PublicApi.GetTrack(long.Parse(trackId));
                string artists = "";
                for (int x = 0; x < track["contributors"]!.Count(); x++)
                {
                    artists += track["contributors"]![x]!["name"]!;
                    if (x != track["contributors"]!.Count() - 1)
                    {
                        artists += ", ";
                    }
                }
                string title = track["title"]!.ToString();
                string album = track["album"]!["title"]!.ToString();
                string isrc = track["isrc"]!.ToString();
                trackDescription = new Tuple<string, string>(isrc, $"{title} ¡] {artists} ({album})");
            } catch { }
            return trackDescription;
        }

        public static string BuildTrackURL(long trackId)
        {
            return $"https://www.deezer.com/track/{trackId}";
        }

        private static string SanitizeTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
                return title;

            string sanitizedTitle = Regex.Replace(title, @"\([^()]*\)|\[[^\[\]]*\]", "");

            sanitizedTitle = Regex.Replace(sanitizedTitle, @"[^a-zA-Z0-9\s]", "");

            sanitizedTitle = Regex.Replace(sanitizedTitle, @"\s+", " ").Trim();

            return sanitizedTitle;
        }

        public static async Task<string> GetTrackIdFromUrlAsync(string url)
        {
            if (RegexUtils.matchDeezerShortLinkUrl(url))
            {
                int count = 1;
                while (RegexUtils.matchDeezerShortLinkUrl(url) && count < 10)
                {
                    url = await HttpUtils.ResolveRedirect(url);
                    count++;
                }
            }

            var cleanUrl = url.Split('?')[0];

            var match = Regex.Match(cleanUrl, @"\/track\/(\d+)");

            if (match.Success)
            {
                return match.Groups[1].Value;

            }
            else
            {
                return "";
            }

        }


    }
}
