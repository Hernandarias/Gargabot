using Gargabot.Parameters;
using Gargabot.Utils.DeezerUtils;
using Gargabot.Utils.Spotify;
using Gargabot.Utils.SpotifyUtils;
using Gargabot.Utils.Youtube;
using Lavalink4NET;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;

namespace Gargabot.Utils.LavalinkUtilities
{
    public static class LavalinkController
    {
        public static async Task<Tuple<string, string>> getArtistIdAndTrackFromName(string name)
        {
            SpotifyController sutils = new SpotifyController(BotParameters.LoadFromJson(BotParameters.GetAppSettingsPath()).spotifyCredentials!);
            return await sutils.GetArtistIdAndTrackFromArtistName(name);
        }

        public static async Task<NewLavalinkTrack> getNextRadioTrack(IAudioService audioService, string videoId, Dictionary<string, bool> history)
        {
            string url = await YoutubeMusicController.GetRecommendationFromVideoId(videoId, history);
            YoutubeVideo ytVideo = await YoutubeController.getVideoInfo(url);
            NewLavalinkTrack nlt = new NewLavalinkTrack();

            if (ytVideo is not null)
            {
                nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration);
                nlt.YoutubeVideoId = ytVideo.Id;
            }

            return nlt;
        }

        public static async Task<NewLavalinkTrack> getNextArtistRadioTrack(IAudioService audioService, string videoId, string artistId, Dictionary<string, bool> history)
        {
            string url = await YoutubeMusicController.GetRecommendationFromVideoIdAndSpotifyArtistId(videoId, artistId, history);
            YoutubeVideo ytVideo = await YoutubeController.getVideoInfo(url);
            NewLavalinkTrack nlt = new NewLavalinkTrack();

            if (ytVideo is not null)
            {
                nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration);
                nlt.YoutubeVideoId = ytVideo.Id;
            }

            return nlt;
        }

        public static async Task<Tuple<List<NewLavalinkTrack>, bool>> loadLavalinkTrack(IAudioService audioService, string search, bool musicTrack, int currentQueueCount)
        {
            var botParams = BotParameters.LoadFromJson(BotParameters.GetAppSettingsPath());
            var nltList = new List<NewLavalinkTrack>();
            bool queueLimitReached = false;

            if (RegexUtils.matchUrl(search))
            {
                YoutubeVideo ytVideo;

                if (RegexUtils.matchYoutubeUrl(search))
                {
                    search = RegexUtils.SanitizeYoutubeUrl(search);
                    ytVideo = await YoutubeController.getVideoInfo(search);

                    if (ytVideo is not null)
                    {
                        var nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration);
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
                        var nlt = new NewLavalinkTrack(yt.Title, yt.Url, yt.Thumbnail, yt.Duration);
                        nlt.YoutubeVideoId = yt.Id;
                        nltList.Add(nlt);
                    }
                }
                else if (RegexUtils.matchSpotifyPlaylistUrl(search) && botParams.useSpotify)
                {
                    SpotifyController sutils = new SpotifyController(botParams.spotifyCredentials!);
                    var tracks = await sutils.GetTracksFromPlaylist(search, null!, 0);

                    queueLimitReached = checkQueueLimit(tracks.Count, currentQueueCount, botParams.perServerQueueLimit);
                    if (queueLimitReached)
                    {
                        return new Tuple<List<NewLavalinkTrack>, bool>(nltList, queueLimitReached);
                    }

                    foreach (SpotifyTrack st in tracks)
                    {
                        var nlt = new NewLavalinkTrack(st.Title, st.Url, string.Empty, string.Empty);
                        nlt.FullTitle = st.FullTitle;
                        nltList.Add(nlt);
                    }
                }
                else if (RegexUtils.matchSpotifyAlbumUrl(search) && botParams.useSpotify)
                {
                    SpotifyController sutils = new SpotifyController(botParams.spotifyCredentials!);
                    var tracks = await sutils.GetTracksFromAlbum(search);

                    queueLimitReached = checkQueueLimit(tracks.Count, currentQueueCount, botParams.perServerQueueLimit);
                    if (queueLimitReached)
                    {
                        return new Tuple<List<NewLavalinkTrack>, bool>(nltList, queueLimitReached);
                    }

                    foreach (SpotifyTrack st in tracks)
                    {
                        var nlt = new NewLavalinkTrack(st.Title, st.Url, string.Empty, string.Empty);
                        nlt.FullTitle = st.FullTitle;
                        nltList.Add(nlt);
                    }
                }
                else if (RegexUtils.matchSpotifySongUrl(search) && botParams.useSpotify)
                {
                    SpotifyController sutils = new SpotifyController(botParams.spotifyCredentials!);
                    string spotifyId = sutils.GetTrackId(search);
                    string ytMusicUrl = "";

                    string isrc = await sutils.GetTrackISRC(spotifyId);
                    if (!string.IsNullOrEmpty(isrc))
                    {
                        ytMusicUrl = await YoutubeMusicController.getYoutubeMusicUrlFromISRC(isrc);
                    }

                    if (string.IsNullOrEmpty(ytMusicUrl))
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
                        if (ytVideo is not null)
                        {
                            var nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration);
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

                    if (!string.IsNullOrEmpty(trackInfo.Item1))
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
                        if (ytVideo is not null)
                        {
                            var nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration);
                            nlt.YoutubeVideoId = ytVideo.Id;
                            nlt.DeezerTrackId = await DeezerController.GetTrackIdFromUrlAsync(search);
                            nlt.FullTitle = search;
                            nltList.Add(nlt);
                        }
                    }
                }
                else
                {
                    var lavalinkTrack = await loadLavalinkTrack(search, audioService, TrackSearchMode.None);
                    if (lavalinkTrack is not null)
                    {
                        nltList.Add(new NewLavalinkTrack(
                            lavalinkTrack.Title,
                            lavalinkTrack.Uri?.ToString() ?? search,
                            string.Empty,
                            string.Empty,
                            lavalinkTrack));
                    }
                }
            }
            else
            {
                switch (botParams.lavalinkCredentials!.searchEngine)
                {
                    case "youtube":
                        YoutubeVideo ytVideo;

                        if (musicTrack)
                        {
                            string ytMusicUrl = "";
                            DeezerTrack dt = await DeezerController.GetTrackFromQueryAsync(search);

                            if (!string.IsNullOrWhiteSpace(dt.Isrc))
                            {
                                ytMusicUrl = await YoutubeMusicController.getYoutubeMusicUrlFromISRC(dt.Isrc);
                            }

                            if (string.IsNullOrEmpty(ytMusicUrl))
                            {
                                ytMusicUrl = await YoutubeMusicController.getYoutubeMusicUrlFromQuery(search);
                            }

                            ytVideo = await YoutubeController.getVideoInfo(ytMusicUrl);
                        }
                        else
                        {
                            ytVideo = await YoutubeController.getFromQuery(search);
                        }

                        if (ytVideo is not null)
                        {
                            var nlt = new NewLavalinkTrack(ytVideo.Title, ytVideo.Url, ytVideo.Thumbnail, ytVideo.Duration);
                            nlt.YoutubeVideoId = ytVideo.Id;
                            nlt.FullTitle = search;
                            nltList.Add(nlt);
                        }
                        break;

                    case "soundcloud":
                        var scTrack = await loadLavalinkTrack(search, audioService, TrackSearchMode.SoundCloud);
                        if (scTrack is not null)
                        {
                            var nlt = new NewLavalinkTrack(scTrack.Title, scTrack.Uri?.ToString() ?? string.Empty,string.Empty,string.Empty,scTrack);
                            nlt.FullTitle = search;
                            nltList.Add(nlt);
                        }
                        break;
                }
            }

            return new Tuple<List<NewLavalinkTrack>, bool>(nltList, queueLimitReached);
        }

        public static async Task<LavalinkTrack?> loadLavalinkTrack(string query, IAudioService audioService, TrackSearchMode searchMode)
        {
            return await audioService.Tracks.LoadTrackAsync(query, searchMode);
        }

        private static bool checkQueueLimit(int itemsToAdd, int currentQueueCount, int queueLimit)
        {
            return currentQueueCount + itemsToAdd > queueLimit;
        }
    }
}