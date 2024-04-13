using AngleSharp.Css;
using DSharpPlus.Lavalink;
using Gargabot.Parameters;
using Gargabot.Utils.Spotify;
using Gargabot.Utils.Youtube;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using YoutubeExplode.Videos;

namespace Gargabot.Utils.LavalinkUtilities
{
    public static class LavalinkUtils
    {
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
                    ytVideo = await YoutubeUtils.getVideoInfo(search);
                    if (ytVideo!= null)
                    {
                        nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration, null, node);
                        nltList.Add(nlt);
                    }                     
                }

               

                else if (RegexUtils.matchYoutubePlaylistUrl(search))
                {
                    var ytVideos = await YoutubeUtils.getVideosFromPlaylist(search);

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
                    SpotifyUtils sutils = new SpotifyUtils(botParams.spotifyCredentials);
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
                            string ytMusicUrl = await YoutubeMusicUtils.getYoutubeMusicUrlFromCompleteTrackName(name, false);
                            ytVideo = await YoutubeUtils.getVideoInfo(ytMusicUrl);
                            if (ytVideo != null)
                            {
                                nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration, null, node);
                                nltList.Add(nlt);
                            }
                        }              
                    }
                }
                else if (RegexUtils.matchSpotifyAlbumUrl(search) && botParams.useSpotify)
                {
                    SpotifyUtils sutils = new SpotifyUtils(botParams.spotifyCredentials);
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
                            string ytMusicUrl = await YoutubeMusicUtils.getYoutubeMusicUrlFromCompleteTrackName(name, false);
                            ytVideo = await YoutubeUtils.getVideoInfo(ytMusicUrl);
                            if (ytVideo != null)
                            {
                                nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration, null, node);
                                nltList.Add(nlt);
                            }
                        }
                    }

                }
                else if (RegexUtils.matchSpotifySongUrl(search) && botParams.useSpotify)
                { 
                    SpotifyUtils sutils = new SpotifyUtils(botParams.spotifyCredentials);
                    search = await sutils.GetTrackTitle(search);
                    if (!string.IsNullOrEmpty(search))
                    {
                        string ytMusicUrl = await YoutubeMusicUtils.getYoutubeMusicUrlFromCompleteTrackName(search, false);
                        ytVideo = await YoutubeUtils.getVideoInfo(ytMusicUrl);
                        if (ytVideo != null)
                        {
                            nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration, null, node);
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
                            string ytMusicUrl = await YoutubeMusicUtils.getYoutubeMusicUrlFromQuery(search);
                            ytVideo = await YoutubeUtils.getVideoInfo(ytMusicUrl);
                        }
                        else 
                        {
                            ytVideo = await YoutubeUtils.getFromQuery(search);
                        }

                        if (ytVideo != null)
                        {
                            nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration, null, node);
                            nltList.Add(nlt);
                        }
                        break;
                    case "soundcloud": //This depends entirely on Lavalink's search engine
                        LavalinkSearchType searchType = LavalinkSearchType.SoundCloud;
                        var lavalinkTrack = await loadLavalinkTrack(search, node, searchType);
                        
                        if (lavalinkTrack != null)
                        {
                            nlt = new NewLavalinkTrack(lavalinkTrack.Title, lavalinkTrack.Uri.ToString(), "", lavalinkTrack.Length.ToString(), lavalinkTrack, node);
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
