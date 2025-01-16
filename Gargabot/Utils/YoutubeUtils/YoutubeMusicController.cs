﻿using Gargabot.Parameters;
using Gargabot.Utils.Spotify;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using YoutubeExplode;
using YoutubeExplode.Common;

namespace Gargabot.Utils.Youtube
{
    public static class YoutubeMusicController
    {
        public static async Task<string> getYoutubeMusicUrlFromCompleteTrackName(string search, bool secondTry)
        {
            string url = "";

            //This checks the Youtube Music search page to get the song url. This is not an easy task because Youtube Music sends the info in a pretty weird way, so we check the gotten the track's title and redo the task up to 3 times to get as close as we can to the actual url.

            try
            {

                string page = await getYotubeMusicSearchPage(search);

                //First videoId in the page (usually a music video)
                var videoIdMatch = Regex.Match(Regex.Unescape(page), "\"videoId\":\"(.*?)\"");
                if (videoIdMatch.Success)
                {
                    url = buildUrl(videoIdMatch.Groups[1].Value.Trim());
                    if (await YoutubeController.checkIfTitlesMatch(search.Substring(0, search.IndexOf("¡]")-1), url))
                    {
                        return url;
                    }
                }

                //First videoId after musicShelfRenderer (tends to be the correct one)
                var trackIdMatch = Regex.Match(Regex.Unescape(page), "musicShelfRenderer.*?\"musicVideoType\":\"(?!MUSIC_VIDEO_TYPE_PODCAST_EPISODE).*?\".*?\"videoId\":\"(.*?)\"");
                url = buildUrl(trackIdMatch.Groups[1].Value.Trim());
                if (await YoutubeController.checkIfTitlesMatch(search.Substring(0, search.IndexOf("¡]") - 1), url))
                {
                    return url;
                }
                else
                {
                    //First playlistId in the page (tends to be the album, but we check it only if the previous check is incorrect because, again, it is usually correct)
                    var playlistMatch = Regex.Match(Regex.Unescape(page), "\"watchPlaylistEndpoint.*?\"playlistId\":\"(.*?)\"");
                    if (playlistMatch.Success)
                    {
                        string auxUrl = await YoutubeController.checkIfPlaylistHasTrack(search.Substring(0, search.IndexOf("¡]") - 1), buildPlaylistUrl(playlistMatch.Groups[1].Value.Trim()));   
                        if (auxUrl != "")
                        {
                            return auxUrl;
                        }
                        else
                        {
                            if(!secondTry)
                            {
                                string newUrl=await getYoutubeMusicUrlFromCompleteTrackName(search.Substring(0, search.LastIndexOf("¡]") - 1), true);
                                if (newUrl != "")
                                {
                                    return newUrl;
                                }
                                else
                                {
                                    return url;
                                }
                            }
                            else
                            {
                                return "";
                            }
                        }
                    }
                }

            }
            catch
            {
                url = "";
            }
            return url;

        }

        public static async Task<string> getYoutubeMusicUrlFromQuery(string search)
        {
            try
            {
                string page = await getYotubeMusicSearchPage(search);
                var trackIdMatch = Regex.Match(Regex.Unescape(page), "\"queueTarget\":.*?\"(videoId)\":\"(.*?)\"");
                if (trackIdMatch.Success)
                {
                    
                    if(await YoutubeController.checkIfVideoIsAutogeneratedByYoutube(buildUrl(trackIdMatch.Groups[2].Value.Trim())))
                    {
                        return buildUrl(trackIdMatch.Groups[2].Value.Trim());
                    }
                    else
                    {
                        trackIdMatch = Regex.Match(Regex.Unescape(page), "musicShelfRenderer.*?\"musicVideoType\":\"(?!MUSIC_VIDEO_TYPE_PODCAST_EPISODE).*?\".*?\"videoId\":\"(.*?)\"");
                        if (trackIdMatch.Success)
                        {
                            return buildUrl(trackIdMatch.Groups[1].Value.Trim());
                        }
                        else
                        {
                            return "";
                        }
                    }   

                }
                else
                {
                    return "";
                }
            }
            catch
            {
                return "";
            }

        }

        public static async Task<string> getYotubeMusicSearchPage(string search)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:108.0) Gecko/20100101 Firefox/108.0");

                return await client.GetStringAsync($"https://music.youtube.com/search?q={Uri.EscapeDataString(search)}");
            }
            catch
            {
                return "";
            }
        }

        public static async Task<string> GetRecommendationFromVideoId(string videoId, Dictionary<string, bool> history)
        {
            string recommendationUrl = "";
            try
            {
                string radioUrl = $"https://music.youtube.com/watch?v={videoId}&list=RD{videoId}";

                YoutubeClient yc = new YoutubeClient();
                var videos = await yc.Playlists.GetVideosAsync(radioUrl);

                if (videos.Count > 0)
                {
                    var random = new Random();
                    var visitedIndices = new HashSet<int>();

                    while (visitedIndices.Count < videos.Count)
                    {
                        int randomIndex = random.Next(videos.Count);
                        if (visitedIndices.Contains(randomIndex))
                            continue;

                        visitedIndices.Add(randomIndex);

                        if (videos[randomIndex].Id != videoId && !history.ContainsKey(videos[randomIndex].Id))
                        {
                            var video = await yc.Videos.GetAsync(videos[randomIndex].Url);
                            if (video.Description.Trim().EndsWith("Auto-generated by YouTube."))
                            {
                                recommendationUrl = buildUrl(video.Id);
                                break;
                            }
                            else if (visitedIndices.Count>=5 || visitedIndices.Count== videos.Count) //last resort
                            {
                                SpotifyController sutils = new SpotifyController(BotParameters.LoadFromJson(BotParameters.GetAppSettingsPath()).spotifyCredentials);
                                Tuple<string, List<string>> spotifyInfo = await sutils.GetTrackAndArtistIdFromTrackName(video.Title);
                                string trackTitle = await sutils.GetTrackTitle(sutils.buildSpotifyUrl(spotifyInfo.Item1));
                                recommendationUrl = await getYoutubeMusicUrlFromCompleteTrackName(trackTitle, false);
                                if (history.ContainsKey(getVideoIdFromUrl(recommendationUrl)))
                                {
                                    continue;
                                }

                                break;
                            }
                            
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
            return recommendationUrl;

        }

        public static async Task<string> GetRecommendationFromVideoIdAndSpotifyArtistId(string videoId, string artistId, Dictionary<string, bool> history)
        {
            string recommendationUrl = "";
            try
            {
                string radioUrl = $"https://music.youtube.com/watch?v={videoId}&list=RD{videoId}";

                YoutubeClient yc = new YoutubeClient();
                var videos = await yc.Playlists.GetVideosAsync(radioUrl);

                // Random shuffle
                videos = videos.OrderBy(a => Guid.NewGuid()).ToList();

                SpotifyController sutils = new SpotifyController(BotParameters.LoadFromJson(BotParameters.GetAppSettingsPath()).spotifyCredentials);

                foreach (var video in videos)
                {
                    if (video.Id == videoId || history.ContainsKey(video.Id))
                        continue;

                    Tuple<string, List<string>> spotifyInfo = await sutils.GetTrackAndArtistIdFromTrackName(video.Title.Substring(0, Math.Min(40, video.Title.Length)));

                    if (spotifyInfo.Item2.Contains(artistId))
                    {
                        var detailedVideo = await yc.Videos.GetAsync(video.Url);
                        if (detailedVideo.Description.Trim().EndsWith("Auto-generated by YouTube."))
                        {
                            recommendationUrl = buildUrl(detailedVideo.Id);
                            break;
                        }

                        string trackTitle = await sutils.GetTrackTitle(sutils.buildSpotifyUrl(spotifyInfo.Item1));

                        recommendationUrl = await getYoutubeMusicUrlFromCompleteTrackName(trackTitle, false);

                        if (history.ContainsKey(getVideoIdFromUrl(recommendationUrl)))
                        {
                            continue;
                        }

                        break;
                    }
                    if (video == videos.Last())
                    {
                        List<string> topTracks = await sutils.GetTopTracksFromArtistId(artistId);
                        topTracks.Shuffle();
                        foreach (var track in topTracks)
                        {
                            recommendationUrl = await getYoutubeMusicUrlFromCompleteTrackName(track, false);
                            if (recommendationUrl != "" && !history.ContainsKey(getVideoIdFromUrl(recommendationUrl)))
                            {
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { }

            return recommendationUrl;
        }



        private static string buildUrl(string videoId)
        {
            return "https://youtube.com/watch?v=" + videoId;
        }
        private static string buildPlaylistUrl(string playlistId)
        {
            return "https://youtube.com/playlist?list=" + playlistId;
        }   
        private static string getVideoIdFromUrl(string url)
        {
            string videoId = "";
            if (url.Contains("watch?v="))
            {
                videoId = url.Substring(url.IndexOf("watch?v=") + 8);
                if (videoId.Contains("&"))
                {
                    videoId = videoId.Substring(0, videoId.IndexOf("&"));
                }
            }
            return videoId;
        }

    }
}
