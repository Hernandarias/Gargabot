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
using Gargabot.Utils.SpotifyUtils;
using System.Diagnostics;
using Gargabot.Utils.DeezerUtils;

namespace Gargabot.Utils.LavalinkUtilities
{
    public static class LavalinkController
    {
        public static async Task<Tuple<string, string>> getArtistIdAndTrackFromName(string name)
        {
            SpotifyController sutils = new SpotifyController(BotParameters.LoadFromJson(BotParameters.GetAppSettingsPath()).spotifyCredentials);
            return await sutils.GetArtistIdAndTrackFromArtistName(name);
        }
        public static async Task<NewLavalinkTrack> getNextRadioTrack(LavalinkNodeConnection node, string videoId, Dictionary<string, bool> history)
        {
            string url = await YoutubeMusicController.GetRecommendationFromVideoId(videoId, history);
            YoutubeVideo ytVideo = await YoutubeController.getVideoInfo(url);
            NewLavalinkTrack nlt = new NewLavalinkTrack();
            if (ytVideo != null)
            {
                nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration, null!, node);
                nlt.YoutubeVideoId = ytVideo.Id;
            }
            return nlt;
        }

        public static async Task<NewLavalinkTrack> getNextArtistRadioTrack(LavalinkNodeConnection node, string videoId, string artistId, Dictionary<string, bool> history)
        {
            string url = await YoutubeMusicController.GetRecommendationFromVideoIdAndSpotifyArtistId(videoId, artistId, history);
            YoutubeVideo ytVideo = await YoutubeController.getVideoInfo(url);
            NewLavalinkTrack nlt = new NewLavalinkTrack();
            if (ytVideo != null)
            {
                nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration, null!, node);
                nlt.YoutubeVideoId = ytVideo.Id;
            }
            return nlt;
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
                    if (ytVideo != null)
                    {
                        nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration, null!, node);
                        nlt.YoutubeVideoId = ytVideo.Id;
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
                        nlt = new NewLavalinkTrack(yt.Title, yt.Url, yt.Thumbnail, yt.Duration, null!, node);
                        nlt.YoutubeVideoId = yt.Id;
                        nltList.Add(nlt);
                    }
                }
                else if (RegexUtils.matchSpotifyPlaylistUrl(search) && botParams.useSpotify)
                {
                    SpotifyController sutils = new SpotifyController(botParams.spotifyCredentials);
                    var tracks = await sutils.GetTracksFromPlaylist(search, null!, 0);

                    queueLimitReached = checkQueueLimit(tracks.Count, currentQueueCount, botParams.perServerQueueLimit);
                    if (queueLimitReached)
                    {
                        return new Tuple<List<NewLavalinkTrack>, bool>(nltList, queueLimitReached);
                    }

                    foreach (SpotifyTrack st in tracks)
                    {
                        nlt = new NewLavalinkTrack(st.Title, st.Url, "", "", null!, node);
                        nlt.FullTitle = st.FullTitle;
                        nltList.Add(nlt);
                    }

                }
                else if (RegexUtils.matchSpotifyAlbumUrl(search) && botParams.useSpotify)
                {
                    SpotifyController sutils = new SpotifyController(botParams.spotifyCredentials);
                    var tracks = await sutils.GetTracksFromAlbum(search);

                    queueLimitReached = checkQueueLimit(tracks.Count, currentQueueCount, botParams.perServerQueueLimit);
                    if (queueLimitReached)
                    {
                        return new Tuple<List<NewLavalinkTrack>, bool>(nltList, queueLimitReached);
                    }

                    foreach (SpotifyTrack st in tracks)
                    {
                        nlt = new NewLavalinkTrack(st.Title, st.Url, "", "", null!, node);
                        nlt.FullTitle = st.FullTitle;
                        nltList.Add(nlt);
                    }

                }
                else if (RegexUtils.matchSpotifySongUrl(search) && botParams.useSpotify)
                {
                    SpotifyController sutils = new SpotifyController(botParams.spotifyCredentials);
                    string spotifyId = sutils.GetTrackId(search);
                    string ytMusicUrl = "";

                    //First we'll try to get the track's ISRC code
                    string isrc = await sutils.GetTrackISRC(spotifyId);
                    if (isrc != "")
                    {
                        ytMusicUrl = await YoutubeMusicController.getYoutubeMusicUrlFromISRC(isrc);
                    }

                    if (string.IsNullOrEmpty(ytMusicUrl)) //If we can't get the ISRC code, we'll try to get the track's title
                    {
                        search = await sutils.GetTrackTitle(search);
                        if (!string.IsNullOrEmpty(search))
                        {
                            ytMusicUrl = await YoutubeMusicController.getYoutubeMusicUrlFromCompleteTrackName(search, false);
                        }
                    }

                    if (!string.IsNullOrEmpty(ytMusicUrl))
                    {
                        ytVideo = await YoutubeController.getVideoInfo(ytMusicUrl);
                        if (ytVideo != null)
                        {
                            nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration, null!, node);
                            nlt.YoutubeVideoId = ytVideo.Id;
                            nlt.SpotifyTrackId = spotifyId;
                            nlt.FullTitle = search;
                            nltList.Add(nlt);
                        }
                    }

                }
                else if (RegexUtils.matchDeezerTrackUrl(search) || RegexUtils.matchDeezerShortLinkUrl(search))
                {
                    Tuple<string, string> trackInfo = await DeezerController.GetTrackDescription(search);
                    string ytMusicUrl = "";
                    if (!string.IsNullOrEmpty(trackInfo.Item1)) //ISRC
                    {
                        ytMusicUrl = await YoutubeMusicController.getYoutubeMusicUrlFromISRC(trackInfo.Item1);
                    }
                    if (string.IsNullOrEmpty(ytMusicUrl))
                    {
                        ytMusicUrl = await YoutubeMusicController.getYoutubeMusicUrlFromCompleteTrackName(trackInfo.Item2, false);
                    }
                    if (!string.IsNullOrEmpty(ytMusicUrl))
                    {
                        ytVideo = await YoutubeController.getVideoInfo(ytMusicUrl);
                        if (ytVideo != null)
                        {
                            nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration, null!, node);
                            nlt.YoutubeVideoId = ytVideo.Id;
                            nlt.DeezerTrackId = await DeezerController.GetTrackIdFromUrlAsync(search);
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
                            string ytMusicUrl = "";
                            //First we'll try to use Deezer to get the track
                            DeezerTrack dt = await DeezerController.GetTrackFromQueryAsync(search);
                            if (dt.Isrc.Trim()!="")
                            {
                                ytMusicUrl = await YoutubeMusicController.getYoutubeMusicUrlFromISRC(dt.Isrc);
                            }
                            
                            if(string.IsNullOrEmpty(ytMusicUrl))
                            {
                                ytMusicUrl = await YoutubeMusicController.getYoutubeMusicUrlFromQuery(search);
                            }

                            ytVideo = await YoutubeController.getVideoInfo(ytMusicUrl);
                        }
                        else 
                        {
                            ytVideo = await YoutubeController.getFromQuery(search);
                        }

                        if (ytVideo != null)
                        {
                            nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration, null!, node);
                            nlt.YoutubeVideoId = ytVideo.Id;
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
            return null!;

        }

        private static bool checkQueueLimit(int itemsToAdd, int currentQueueCount, int queueLimit)
        {
           
            return currentQueueCount+ itemsToAdd > queueLimit;
        }


    }
}
