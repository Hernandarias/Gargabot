using AngleSharp.Css;
using DSharpPlus.Lavalink;
using Gargabot.Parameters;
using Gargabot.Utils.Youtube;
using Gargabot.Utils.Spotify;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using YoutubeExplode.Videos;
using System.ComponentModel.Design;

namespace Gargabot.Utils.LavalinkUtilities
{
    public static class LavalinkController
    {
        public static async Task<Tuple<string, string>> getArtistIdAndTrackFromName(string name)
        {
            SpotifyController sutils = new SpotifyController(BotParameters.LoadFromJson(BotParameters.GetAppSettingsPath()).spotifyCredentials);
            return await sutils.GetArtistIdAndTrackFromArtistName(name);
        }
        public static async Task<Tuple<NewLavalinkTrack, string>> getNextRadioTrack(LavalinkNodeConnection node, bool artistRadio, Tuple<string, List<string>, bool> trackIdentifier, Dictionary<string, bool> history)
        {
            SpotifyController sutils = new SpotifyController(BotParameters.LoadFromJson(BotParameters.GetAppSettingsPath()).spotifyCredentials);
            Tuple<string, List<string>> ids;
            if (!trackIdentifier.Item3)
            {
                ids = await sutils.GetTrackAndArtistIdFromTrackName(trackIdentifier.Item1);
                if (!history.ContainsKey(ids.Item1))
                {
                    history.Add(ids.Item1, true);
                }
            }
            else
            {
                ids = new Tuple<string, List<string>>(trackIdentifier.Item1, trackIdentifier.Item2);
            }

            if (string.IsNullOrEmpty(ids.Item1))
            {
                return null;
            }

            string mode = "";
            if (artistRadio)
            {
                mode = "SAME_ARTIST";
            }
            else
            {
                Random random = new Random();
                double d = random.NextDouble();
                if (d >= 0 && d < 0.5)
                {
                    mode = "SAME_ARTIST";
                }
                else if (d >= 0.5 && d <= 1)
                {
                    mode = "MOST_POPULAR";
                }
            }


            Tuple<string, List<string>> recommendation = await sutils.GetTrackRecommendation(ids, mode, history);
            Tuple<List<NewLavalinkTrack>, bool> result = await loadLavalinkTrack(node, recommendation.Item1, true, 0);
            if (result.Item1.Count > 0)
            {
                result.Item1.First().SpotifyTrackId = sutils.GetTrackId(recommendation.Item1);
                result.Item1.First().SpotifyArtistsIds = recommendation.Item2;
                return new Tuple<NewLavalinkTrack, string>(result.Item1.First(), ids.Item1);
            }
            else
            {
                return null;
            }
        }

        public static async Task<Tuple<List<NewLavalinkTrack>, bool>> loadLavalinkTrack(LavalinkNodeConnection node, string search, bool musicTrack, int currentQueueCount)
        {
            NewLavalinkTrack nlt;

            var botParams = BotParameters.LoadFromJson(BotParameters.GetAppSettingsPath());

            List<NewLavalinkTrack> nltList = new List<NewLavalinkTrack>();
            bool queueLimitReached = false;


            if (RegexUtils.matchUrl(search))
            {
                YoutubeVideo ytVideo;
                if (RegexUtils.matchYoutubeUrl(search))
                {
                    search = RegexUtils.SanitizeYoutubeUrl(search);
                    ytVideo = await YoutubeController.getVideoInfo(search);
                    if (ytVideo!= null)
                    {
                        nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration, null, node);
                        nltList.Add(nlt);
                    }                     
                }

               

                else if (RegexUtils.matchYoutubePlaylistUrl(search))
                {
                    var ytVideos = await YoutubeController.getVideosFromPlaylist(search);

                    queueLimitReached = checkQueueLimit(ytVideos.Count, currentQueueCount, botParams.perServerQueueLimit);
                    if (queueLimitReached)
                    {
                        return new Tuple<List<NewLavalinkTrack>, bool>(nltList, queueLimitReached);
                    }

                    foreach (YoutubeVideo yt in ytVideos)
                    {
                        nlt = new NewLavalinkTrack(yt.Title, yt.Url, yt.Thumbnail, yt.Duration, null, node);
                        nltList.Add(nlt);
                    }
                }
                else if (RegexUtils.matchSpotifyPlaylistUrl(search) && botParams.useSpotify)
                {
                    SpotifyController sutils = new SpotifyController(botParams.spotifyCredentials);
                    var trackNames = await sutils.GetTracksFromPlaylist(search);

                    queueLimitReached = checkQueueLimit(trackNames.Count, currentQueueCount, botParams.perServerQueueLimit);
                    if (queueLimitReached)
                    {
                        return new Tuple<List<NewLavalinkTrack>, bool>(nltList, queueLimitReached);
                    }

                    foreach (string name in trackNames)
                    {
                        if(!string.IsNullOrEmpty(name))
                        {
                            string ytMusicUrl = await YoutubeMusicController.getYoutubeMusicUrlFromCompleteTrackName(name, false);
                            ytVideo = await YoutubeController.getVideoInfo(ytMusicUrl);
                            if (ytVideo != null)
                            {
                                nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration, null, node);
                                nlt.FullTitle = name;
                                nltList.Add(nlt);
                            }
                        }              
                    }
                }
                else if (RegexUtils.matchSpotifyAlbumUrl(search) && botParams.useSpotify)
                {
                    SpotifyController sutils = new SpotifyController(botParams.spotifyCredentials);
                    var trackNames = await sutils.GetTracksFromAlbum(search);

                    queueLimitReached = checkQueueLimit(trackNames.Count, currentQueueCount, botParams.perServerQueueLimit);
                    if(queueLimitReached)
                    {
                        return new Tuple<List<NewLavalinkTrack>, bool>(nltList, queueLimitReached);
                    }

                    foreach (string name in trackNames)
                    {
                        if (!string.IsNullOrEmpty(name))
                        {
                            string ytMusicUrl = await YoutubeMusicController.getYoutubeMusicUrlFromCompleteTrackName(name, false);
                            ytVideo = await YoutubeController.getVideoInfo(ytMusicUrl);
                            if (ytVideo != null)
                            {
                                nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration, null, node);
                                nlt.FullTitle = name;
                                nltList.Add(nlt);
                            }
                        }
                    }

                }
                else if (RegexUtils.matchSpotifySongUrl(search) && botParams.useSpotify)
                { 
                    SpotifyController sutils = new SpotifyController(botParams.spotifyCredentials);
                    string spotifyId = sutils.GetTrackId(search);
                    search = await sutils.GetTrackTitle(search);
                    if (!string.IsNullOrEmpty(search))
                    {
                        string ytMusicUrl = await YoutubeMusicController.getYoutubeMusicUrlFromCompleteTrackName(search, false);
                        ytVideo = await YoutubeController.getVideoInfo(ytMusicUrl);
                        if (ytVideo != null)
                        {
                            nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration, null, node);
                            nlt.SpotifyTrackId = spotifyId;
                            nlt.FullTitle = search;
                            nltList.Add(nlt);
                        }
                    }
                }
                else
                {
                    var lavalinkTrack = await loadLavalinkTrack(search, node, LavalinkSearchType.Plain);
                    if (lavalinkTrack != null)
                    {
                        nlt = new NewLavalinkTrack(lavalinkTrack.Title, lavalinkTrack.Uri.ToString(), "", lavalinkTrack.Length.ToString(), lavalinkTrack, node);
                        nltList.Add(nlt);
                    }
                }
            }
            else
            {
                switch (botParams.lavalinkCredentials.searchEngine)
                {
                    case "youtube":
                        YoutubeVideo ytVideo;
                        if (musicTrack)
                        { 
                            string ytMusicUrl = await YoutubeMusicController.getYoutubeMusicUrlFromQuery(search);
                            ytVideo = await YoutubeController.getVideoInfo(ytMusicUrl);
                        }
                        else 
                        {
                            ytVideo = await YoutubeController.getFromQuery(search);
                        }

                        if (ytVideo != null)
                        {
                            nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration, null, node);
                            nlt.FullTitle = search;
                            nltList.Add(nlt);
                        }
                        break;
                    case "soundcloud": //This depends entirely on Lavalink's search engine
                        LavalinkSearchType searchType = LavalinkSearchType.SoundCloud;
                        var lavalinkTrack = await loadLavalinkTrack(search, node, searchType);
                        
                        if (lavalinkTrack != null)
                        {
                            nlt = new NewLavalinkTrack(lavalinkTrack.Title, lavalinkTrack.Uri.ToString(), "", lavalinkTrack.Length.ToString(), lavalinkTrack, node);
                            nlt.FullTitle = search;
                            nltList.Add(nlt);
                        }
                        break;
                }

            }

            return new Tuple<List<NewLavalinkTrack>, bool>(nltList, queueLimitReached);

        }


        public static async Task<LavalinkTrack> loadLavalinkTrack(string query, LavalinkNodeConnection node, LavalinkSearchType lst)
        {
            var loadResult = await node.Rest.GetTracksAsync(query, lst);
            if (!(loadResult.LoadResultType == LavalinkLoadResultType.LoadFailed || loadResult.LoadResultType == LavalinkLoadResultType.NoMatches))
            {
                return loadResult.Tracks.First();

            }
            return null;

        }

        private static bool checkQueueLimit(int itemsToAdd, int currentQueueCount, int queueLimit)
        {
           
            return currentQueueCount+ itemsToAdd > queueLimit;
        }


    }
}
