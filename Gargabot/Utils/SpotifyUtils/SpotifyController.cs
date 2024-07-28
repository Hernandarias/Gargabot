using AngleSharp.Dom;
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
            string endpoint = "https://api.spotify.com/v1/search?q=" + Uri.EscapeDataString(name) + "&type=track&limit=1";
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
                    }
                }
            }
            return new Tuple<string, List<string>>(trackId, artistIds);
        }

        public async Task<Tuple<string, List<string>>> GetTrackRecommendation(Tuple<string, List<string>> baseTuple, string mode, Dictionary<string, bool> history)
        {
            List<string> artistIds = new List<string>();
            Tuple<string, List<string>> result = new Tuple<string, List<string>>("", artistIds);
            string token = await GetToken();
            if (token == null)
                return result;
            string chosenTrack = "";
            string endpoint = "";
            if(mode == "SAME_ARTIST" && baseTuple.Item2.Count>0)
            {
                string seedTracks = baseTuple.Item1;
                int x = 1;
                foreach (string trackId in history.Keys)
                {
                    x++;
                    seedTracks += "," + trackId;
                    if (x >= 4)
                        break;
                }

                endpoint = "https://api.spotify.com/v1/recommendations?seed_tracks=" + seedTracks +"&seed_artists=" + baseTuple.Item2.FirstOrDefault() + "&limit=70";
            }
            else
            {
                endpoint = "https://api.spotify.com/v1/recommendations?seed_tracks=" + baseTuple.Item1 + "&limit=50";
            }
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
                        return result;
                    
                    int maxPopularity = 0;
                    foreach (var item in responseJson["tracks"])
                    {
                        string trackId = (string)item["id"];
                        if(trackId == baseTuple.Item1)
                        {
                            continue;
                        }
                        if (history.ContainsKey(trackId))
                        {
                            continue;
                        }
                        bool validTrackForArtistMode = false;
                        List<string> artistIdsForTrack = new List<string>();
                        foreach (var artist in item["artists"])
                        {
                            artistIdsForTrack.Add((string)artist["id"]);
                            foreach(string artistId in baseTuple.Item2)
                            {
                                if ((string)artist["id"] == artistId)
                                {
                                    validTrackForArtistMode = true;
                                    break;
                                }
                            }
                            if(validTrackForArtistMode)
                            {
                                break;
                            }
                        }
                        if ((int)item["popularity"] > maxPopularity)
                        {
                            if (mode == "SAME_ARTIST" && validTrackForArtistMode)
                            {
                                maxPopularity = (int)item["popularity"];
                                chosenTrack = buildSpotifyUrl(trackId);
                                artistIds = artistIdsForTrack;
                                Random random = new Random();
                                if (random.Next(0, 5) == 1)
                                    break;
                            }
                            else if (mode == "MOST_POPULAR")
                            {
                                maxPopularity = (int)item["popularity"];
                                chosenTrack = buildSpotifyUrl(trackId);
                                artistIds = artistIdsForTrack;  
                            }
                        }

                    }
                    
                }
            }
            if (chosenTrack != "")
            {
                result = new Tuple<string, List<string>>(chosenTrack, artistIds);
                return result;
            }
            else
            {
                if(mode == "SAME_ARTIST")
                {
                    string popularTrack = await GetRandomTopTrackFromArtistId(baseTuple.Item2.FirstOrDefault());
                    if (!history.ContainsKey(popularTrack))
                    {
                        return new Tuple<string, List<string>>(buildSpotifyUrl(popularTrack), baseTuple.Item2);
                    }
                    return await GetTrackRecommendation(baseTuple, "MOST_POPULAR", history);
                }
                else
                {
                    return await GetTrackRecommendation(baseTuple, "SAME_ARTIST", history);
                }   
            }
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

        private string buildSpotifyUrl(string id)
        {
            return "https://open.spotify.com/track/" + id;
        }


    }
}
