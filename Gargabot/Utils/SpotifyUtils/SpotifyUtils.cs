using AngleSharp.Dom;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode.Playlists;

namespace Gargabot.Utils.Spotify
{
    public class SpotifyUtils
    {
        SpotifyCredentials spotifyCredentials;
        public SpotifyUtils(SpotifyCredentials spotifyCredentials)
        {
            this.spotifyCredentials = spotifyCredentials;
        }

        public async Task<string> GetToken()
        {
            string tokenEndpoint = "https://accounts.spotify.com/api/token";

            using (HttpClient client = new HttpClient())
            {
                var parameters = new Dictionary<string, string>
                {
                    { "grant_type", "client_credentials" },
                    { "client_id", spotifyCredentials.clientId },
                    { "client_secret",spotifyCredentials.clientSecret }
                };

                var formContent = new FormUrlEncodedContent(parameters);

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri(tokenEndpoint),
                    Content = formContent
                };
                request.Headers.Add("Accept", "application/json");

                HttpResponseMessage response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    JObject responseJSON = JObject.Parse(responseBody);
                    if (responseJSON != null)
                        return (string)responseJSON["access_token"];
                    else
                        return "";

                }
                else
                {
                    return "";
                }
            }
        }

        public async Task<List<string>> GetTracksFromPlaylist(string playlistUrl)
        {
            List<string> tracks = new List<string>();
            string playlistId = "";
            if (string.IsNullOrEmpty(playlistUrl))
                return tracks;
            else
            {
                var uri = new Uri(playlistUrl);
                var segments = uri.AbsolutePath.Split('/');
                int playlistIndex = Array.IndexOf(segments, "playlist");
                if (playlistIndex >= 0 && playlistIndex < segments.Length - 1)
                {
                    playlistId = segments[playlistIndex + 1];
                    if (playlistId.Contains("?"))
                    {
                        playlistId = playlistId.Split('?')[0];
                    }
                }
            }

            string token = await GetToken();

            if (token == null)
                return tracks;

            string endpoint = "https://api.spotify.com/v1/playlists/" + playlistId;

            using (HttpClient client = new HttpClient())
            {

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(endpoint)
                };
                request.Headers.Add("Authorization", "Bearer " + token);

                HttpResponseMessage response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (responseBody == "")
                        return tracks;

                    var jsonObject = JObject.Parse(responseBody);

                    foreach (var item in jsonObject["tracks"]["items"])
                    {
                        var track = item["track"];
                        var songName = track.Value<string>("name");
                        var artists = track["artists"].Select(a => a.Value<string>("name")).ToList();
                        var album = track["album"];

                        string albumName = album.Value<string>("name");

                        string trackTitle = songName + " ¡] " + string.Join(", ", artists) + $" ¡] ({albumName})" ;
                        tracks.Add(trackTitle);
                        
                    }

                }

            }
            return tracks;

        }

        public async Task<List<string>> GetTracksFromAlbum(string albumUrl)
        {
            List<string> tracks = new List<string>();
            string albumId = "";
            if (string.IsNullOrEmpty(albumUrl))
                return tracks;
            else
            {
                var uri = new Uri(albumUrl);
                var segments = uri.AbsolutePath.Split('/');
                int playlistIndex = Array.IndexOf(segments, "album");
                if (playlistIndex >= 0 && playlistIndex < segments.Length - 1)
                {
                    albumId = segments[playlistIndex + 1];
                    if (albumId.Contains("?"))
                    {
                        albumId = albumId.Split('?')[0];
                    }
                }
            }

            string token = await GetToken();

            if (token == null)
                return tracks;

            string endpoint = "https://api.spotify.com/v1/albums/" + albumId;

            using (HttpClient client = new HttpClient())
            {

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(endpoint)
                };
                request.Headers.Add("Authorization", "Bearer " + token);

                HttpResponseMessage response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (responseBody == "")
                        return tracks;

                    var jsonObject = JObject.Parse(responseBody);

                    string albumName = (string)jsonObject["name"];


                    foreach (var item in jsonObject["tracks"]["items"])
                    {
                        var songName = item.Value<string>("name");
                        var artists = item["artists"].Select(a => a.Value<string>("name")).ToList();

                        string trackTitle = songName + " ¡] " + string.Join(", ", artists) + $" ¡] ({albumName})";


                        tracks.Add(trackTitle);

                    }



                }

            }

            return tracks;

        }

        public async Task<string> GetTrackTitle(string trackUrl)
        {
            string trackId = "";
            if (string.IsNullOrEmpty(trackUrl))
                return "";
            else
            {
                var uri = new Uri(trackUrl);
                var segments = uri.AbsolutePath.Split('/');
                int trackIndex = Array.IndexOf(segments, "track");
                if (trackIndex >= 0 && trackIndex < segments.Length - 1)
                {
                    trackId= segments[trackIndex + 1];
                    if (trackId.Contains("?"))
                    {
                        trackId = trackId.Split('?')[0];
                    }   
                }
            }

            string token = await GetToken();

            if (token == null)
                return "";

            string endpoint = "https://api.spotify.com/v1/tracks/" + trackId;

            using (HttpClient client = new HttpClient())
            {

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(endpoint)
                };
                request.Headers.Add("Authorization", "Bearer " + token);

                HttpResponseMessage response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    JObject responseJson = JObject.Parse(responseBody);
                    if (responseJson == null)
                        return "";

                    string trackSearchTitle = "";

                    string trackTitle = (string)responseJson["name"];

                    trackSearchTitle += trackTitle + " ¡] ";

                    JArray artistsArray = (JArray)responseJson["artists"];
                    foreach (JObject artist in artistsArray)
                    {
                        string artistName = (string)artist["name"];
                        trackSearchTitle += artistName + ", ";
                    }

                    var album = responseJson["album"];
                    string albumName = album.Value<string>("name");

                    trackSearchTitle = trackSearchTitle.Substring(0, trackSearchTitle.Length - 2) + $" ¡] ({albumName})";
                    return trackSearchTitle;
                }
                else
                {
                    return "";
                }
            }
        }




    }
}
