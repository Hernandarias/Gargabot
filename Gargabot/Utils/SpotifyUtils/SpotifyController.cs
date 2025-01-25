using AngleSharp.Dom;
using Gargabot.Utils.SpotifyUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode.Playlists;

namespace Gargabot.Utils.Spotify
{
    public class SpotifyController
    {
        SpotifyCredentials spotifyCredentials;
        public SpotifyController(SpotifyCredentials spotifyCredentials)
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

        public async Task<List<SpotifyTrack>> GetTracksFromPlaylist(string playlistUrl, List<SpotifyTrack> lst, int offset)
        {
            List<SpotifyTrack> tracks = new List<SpotifyTrack>();
            if(lst!=null)
            {
                tracks = lst;
            }
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

            string endpoint = "https://api.spotify.com/v1/playlists/" + playlistId + "/tracks?offset=" + offset + "&limit=50";

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

                    foreach (var item in jsonObject["items"])
                    {
                        var track = item["track"];
                        if(string.IsNullOrEmpty(track.ToString()))
                        {
                            tracks.Clear();
                            break;
                        }
                        var songName = track.Value<string>("name");
                        var artists = track["artists"].Select(a => a.Value<string>("name")).ToList();
                        var album = track["album"];

                        string albumName = album.Value<string>("name");

                        string trackTitle = songName + " ¡] " + string.Join(", ", artists) + $" ¡] ({albumName})" ;
                        
                        SpotifyTrack spotifyTrack = new SpotifyTrack(songName, trackTitle, buildSpotifyUrl(track.Value<string>("id")));
                        tracks.Add(spotifyTrack);
                    }

                }

            }
           
            if(tracks.Count-offset == 50)
            {
                return await GetTracksFromPlaylist(playlistUrl, tracks, offset+50);
            }
            else
            {
                if (tracks.Count <= 0)
                {
                    string api = $"https://api.spotifydown.com/tracks/playlist/{playlistId}";

                    using (HttpClient client = new HttpClient())
                    {
                        try
                        {
                            client.DefaultRequestHeaders.Add("Referer", "https://spotifydown.com/");
                            client.DefaultRequestHeaders.Add("Origin", "https://spotifydown.com");

                            HttpResponseMessage response = await client.GetAsync(api);
                            response.EnsureSuccessStatusCode();

                            string responseContent = await response.Content.ReadAsStringAsync();
                            var jsonResponse = JObject.Parse(responseContent);

                            if (jsonResponse["success"]?.Value<bool>() == true)
                            {
                                var trackList = jsonResponse["trackList"];
                                foreach (var item in trackList)
                                {
                                    string songName = item["title"]?.ToString();
                                    string artists = item["artists"]?.ToString();
                                    string albumName = item["album"]?.ToString();

                                    string trackTitle = $"{songName} ¡] {artists} ¡] ({albumName})";
                                    string trackUrl = buildSpotifyUrl(item["id"]?.ToString());

                                    SpotifyTrack spotifyTrack = new SpotifyTrack(songName, trackTitle, trackUrl);
                                    tracks.Add(spotifyTrack);
                                }
                            }
                        }
                        catch { }
                    }
                }
                return tracks;
            }

        }

        public async Task<List<SpotifyTrack>> GetTracksFromAlbum(string albumUrl)
        {
            List<SpotifyTrack> tracks = new List<SpotifyTrack>();
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

                        SpotifyTrack spotifyTrack = new SpotifyTrack(songName, trackTitle, buildSpotifyUrl(item.Value<string>("id")));
                        tracks.Add(spotifyTrack);

                    }



                }

            }

            return tracks;

        }

        public async Task<string> GetTrackTitle(string trackUrl)
        {
            string token = await GetToken();

            if (token == null)
                return "";

            string endpoint = "https://api.spotify.com/v1/tracks/" + GetTrackId(trackUrl);

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

        public async Task<Tuple<string, List<string>>> GetTrackAndArtistIdFromTrackName(string name)
        {
            string trackId = "";
            List<string> artistIds = new List<string>();
            string token = await GetToken();
            if (token == null)
                return new Tuple<string, List<string>>("", []);
            string endpoint = "https://api.spotify.com/v1/search?q=" + Uri.EscapeDataString(name) + "&type=track";
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
                        return new Tuple<string, List<string>>("", []);
                    JArray tracks = (JArray)responseJson["tracks"]["items"];

                    if (tracks.Count > 0)
                    {
                        trackId = (string)tracks[0]["id"];
                        foreach (var artist in tracks[0]["artists"])
                        {
                            artistIds.Add((string)artist["id"]);
                        }

                        foreach (var track in tracks)
                        {
                            string trackName = (string)track["name"];
                            string trackNameNormalized = new string(trackName.Where(c => char.IsLetterOrDigit(c)).ToArray()).ToLower();
                            string nameNormalized = new string(name.Where(c => char.IsLetterOrDigit(c)).ToArray()).ToLower();
                            if (trackNameNormalized.Contains(nameNormalized))
                            {
                                trackId = (string)track["id"];
                                artistIds = new List<string>();
                                foreach (var artist in track["artists"])
                                {
                                    artistIds.Add((string)artist["id"]);
                                }
                                break;
                            }
                        }
                    }
                }
            }
            return new Tuple<string, List<string>>(trackId, artistIds);
        }

        

        public async Task<Tuple<string, string>> GetArtistIdAndTrackFromArtistName(string name)
        {
            string artistId = "";
            string trackId = "";
            string token = await GetToken();
            string endpoint = "https://api.spotify.com/v1/search?q=" + Uri.EscapeDataString(name) + "&type=artist&limit=1";
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
                        return new Tuple<string, string>("", "");
                    JArray artists = (JArray)responseJson["artists"]["items"];
                    if (artists.Count > 0)
                    {
                        artistId = (string)artists[0]["id"];
                    }
                }
            }   
            token = await GetToken();
            if (artistId != "")
            {
                trackId = await GetRandomTopTrackFromArtistId(artistId);
            }   
            return new Tuple<string, string>(artistId, buildSpotifyUrl(trackId));
        }   

        public async Task<string> GetRandomTopTrackFromArtistId(string artistId)
        {
            string token = await GetToken();
            string endpoint = "https://api.spotify.com/v1/artists/" + artistId + "/top-tracks";
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
                    JArray tracks = (JArray)responseJson["tracks"];
                    if (tracks.Count > 0)
                    {
                        Random random = new Random();
                        int randomIndex = random.Next(tracks.Count);
                        return (string)tracks[randomIndex]["id"];
                    }
                }
            }
            return "";
        }

        public async Task<List<string>> GetTopTracksFromArtistId(string artistId)
        {
            string token = await GetToken();
            string endpoint = "https://api.spotify.com/v1/artists/" + artistId + "/top-tracks";
            List<string> trackTitles = new List<string>();
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
                        return trackTitles;

                    JArray tracks = (JArray)responseJson["tracks"];
                    if (tracks.Count > 0)
                    {
                        foreach (var track in tracks)
                        {
                            string trackSearchTitle = "";

                            string trackTitle = (string)track["name"];
                            trackSearchTitle += trackTitle + " ¡] ";

                            JArray artistsArray = (JArray)track["artists"];
                            foreach (JObject artist in artistsArray)
                            {
                                string artistName = (string)artist["name"];
                                trackSearchTitle += artistName + ", ";
                            }

                            JObject album = (JObject)track["album"];
                            string albumName = album.Value<string>("name");

                            trackSearchTitle = trackSearchTitle.Substring(0, trackSearchTitle.Length - 2) + $" ¡] ({albumName})";

                            trackTitles.Add(trackSearchTitle);

                        }
                    }
                }
            }
            return trackTitles;
        }

        public string GetTrackId(string trackUrl)
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
                    trackId = segments[trackIndex + 1];
                    if (trackId.Contains("?"))
                    {
                        trackId = trackId.Split('?')[0];
                    }
                }
            }
            return trackId;
        }

        public string buildSpotifyUrl(string id)
        {
            return "https://open.spotify.com/track/" + id;
        }


    }
}
