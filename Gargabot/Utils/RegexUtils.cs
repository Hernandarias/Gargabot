using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Gargabot.Utils
{
    public static class RegexUtils
    {
        public static bool matchUrl(string input)
        {
            Regex url = new Regex(@"^(http[s]?:\/\/(www\.)?|ftp:\/\/(www\.)?|www\.){1}([0-9A-Za-z-\.@:%_\+~#=]+)+((\.[a-zA-Z]{2,3})+)(\/(.)*)?(\?(.)*)?");
            return url.IsMatch(input);
        }

        public static bool matchSpotifySongUrl(string input)
        {
            Regex spotifyUrl1 = new Regex(@"https:\/\/open\.spotify\.com\/[^\/]+\/track\/([^\/?]+)(?:\?.*)?$"); //Example: https://open.spotify.com/intl-es/track/6ftgxRFnXKiyuKtpdYDQSz?si=85db724c07ad4f89
            Regex spotifyUrl2 = new Regex(@"https:\/\/open\.spotify\.com\/track\/([^\/?]+)(?:\?.*)?$"); //Example: https://open.spotify.com/track/3j5zNcb0aSk7dx3W3mAKzw?si=0877d177d4fb4324
            return (spotifyUrl1.IsMatch(input) || spotifyUrl2.IsMatch(input));
            
        }

        public static bool matchYoutubeUrl(string input)
        {
            Regex regex = new Regex(@"^(https?://)?(www\.)?(youtube\.com|youtu\.be|music\.youtube\.com)/watch\?v=[A-Za-z0-9_-]{11}(&[A-Za-z0-9_-]+=[A-Za-z0-9_%]*)*$");
            return regex.IsMatch(input);
        }

        public static bool matchYoutubePlaylistUrl(string input)
        {
            Regex regex = new Regex(@"https:\/\/www\.youtube\.com\/playlist\?list=[A-Za-z0-9_-]+");
            return regex.IsMatch(input);
        }

        public static bool matchSpotifyPlaylistUrl(string input)
        {
            Regex regex = new Regex(@"https:\/\/open\.spotify\.com\/playlist\/([^\/?]+)(?:\?.*)?$");
            return regex.IsMatch(input);
        }

        public static bool matchSpotifyAlbumUrl(string input)
        {
            Regex regex = new Regex(@"https:\/\/open\.spotify\.com\/album\/([^\/?]+)(?:\?.*)?$");
            Regex regex1 = new Regex(@"https:\/\/open\.spotify\.com\/[^\/]+\/album\/([^\/?]+)(?:\?.*)?$");
            return regex.IsMatch(input) || regex1.IsMatch(input);
        }

        public static string SanitizeYoutubeUrl(string input)
        {
            Uri uri = new Uri(input);
            string videoId = string.Empty;

            if (uri.Host.Contains("youtube.com"))
            {
                var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
                videoId = queryParams["v"];
            }
            else if (uri.Host.Contains("youtu.be"))
            {
                videoId = uri.AbsolutePath.Trim('/');
            }
            return $"https://www.youtube.com/watch?v={videoId}";
        }
    }
}
