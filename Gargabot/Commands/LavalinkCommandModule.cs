using AngleSharp.Css;
using AngleSharp.Dom;
using AngleSharp.Text;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.Lavalink;
using DSharpPlus.Lavalink.EventArgs;
using Gargabot.AudioSessions;
using Gargabot.Messages;
using Gargabot.Utils;
using Gargabot.Utils.DiscordUtils;
using Gargabot.Utils.LavalinkUtilities;
using Gargabot.Utils.Spotify;
using Gargabot.Utils.Youtube;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Gargabot.Commands
{
    public class LavalinkCommandModule : UniversalCommandModule
    {
        private Dictionary<ulong, LavalinkVoiceSession> perServerSession = new Dictionary<ulong, LavalinkVoiceSession>();

        [Command("play")]
        public async Task Play(CommandContext ctx, [RemainingText] string search)
        {
            if (string.IsNullOrEmpty(search) || search.Trim()=="")
            {
                return;
            }

            if (ctx.Member.VoiceState == null)
            {
                await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NOT_IN_A_VOICE_CHANNEL)));
                return;
            }
            var lava = ctx.Client.GetLavalink();
            var node = lava.ConnectedNodes.Values.First();

            var connection = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
            ulong channelId = ctx.Channel.Id;
            DiscordChannel channel = null;

            if (AreBotAndCallerInTheSameChannel(ctx) || node.GetGuildConnection(ctx.Member.VoiceState.Guild) == null)
            {
                if (!lava.ConnectedNodes.Any())
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                    return;
                }

                ulong serverId = ctx.Guild.Id;

                if (!perServerSession.ContainsKey(serverId))
                {
                    LavalinkVoiceSession vs = new LavalinkVoiceSession();
                    perServerSession.Add(serverId, vs);
                    perServerSession[serverId].Joined = false;
                    perServerSession[serverId].CallerTextChannelId = channelId;
                    perServerSession[serverId].HeavyOperationOngoing = false;
                }
                else if(perServerSession[serverId].HeavyOperationOngoing)
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.HEAVY_OPERATION_ONGOING)));
                    return;
                }
                else if (perServerSession[serverId].Queue.Count+1 > botParams.perServerQueueLimit)
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.QUEUE_LIMIT_REACHED)));
                    return;
                }
                else if (perServerSession[serverId].RadioMode || perServerSession[serverId].ArtistRadioMode)
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.RADIO_MODE_IS_ENABLED)));
                    return;
                }

                bool firstJoin = false;
                bool validJoinAudio = false;

                if (connection == null)
                {
                    await node.ConnectAsync(ctx.Member.VoiceState.Channel);
                    firstJoin = true;
                    perServerSession[serverId].Joined = true;

                }

                connection = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
                channel = connection.Guild.GetChannel(channelId);

                if (firstJoin)
                    connection.PlaybackFinished += OnPlaybackFinished;


                bool isMusic = false;
                if(search.StartsWith("[!music]"))
                {
                    search = search.Remove(0, 8);
                    isMusic = true;
                }
                else if (search.StartsWith("[!artistradio]") && firstJoin)
                {
                    if(!isSpotifyEnabled())
                    {
                        await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NO_SPOTIFY_CREDENTIALS)));
                        return;
                    }
                    search = search.Remove(0, 14);  
                    //Get artist ID and random track of that artist
                    Tuple<string, string> artistIdAndTrack = await LavalinkController.getArtistIdAndTrackFromName(search);
                    if (artistIdAndTrack.Item1 != null && artistIdAndTrack.Item2 != null)
                    {
                        perServerSession[serverId].ArtistRadioArtistId = artistIdAndTrack.Item1;
                        search = artistIdAndTrack.Item2;
                        perServerSession[serverId].ArtistRadioMode = true;
                        perServerSession[serverId].CurrentRadioHistory = new Dictionary<string, bool>();
                    }
                    else
                    {
                        await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.ARTIST_NOT_FOUND)));
                        return;
                    }
                }
                


                perServerSession[serverId].HeavyOperationOngoing = true;

                var tuple = await LavalinkController.loadLavalinkTrack(node, search, isMusic, perServerSession[serverId].Queue.Count);

                perServerSession[serverId].HeavyOperationOngoing = false;

                if (tuple.Item2)
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.QUEUE_LIMIT_REACHED)));
                    return;
                }

                List<NewLavalinkTrack> nltList = tuple.Item1;

                if (nltList.Count==0)
                {

                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NO_TRACKS_FOUND_FOR_SEARCH, search)));
                    if (perServerSession[serverId].Joined && connection.CurrentState.CurrentTrack == null && perServerSession[serverId].Queue.Count <= 0)
                        await StopCommand(ctx);
                    return;
                }   

                foreach (NewLavalinkTrack nlt in nltList)
                {

                    perServerSession[serverId].Queue.AddLast(nlt);
                    if(nltList.Count==1)
                        await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(nlt.Url, nlt.FinalTitle, messageManager.GetMessage(Message.ADDED_TO_QUEUE_IN_POSITION, perServerSession[serverId].Queue.Count)));
                }

                if (nltList.Count > 1)
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.MULTIPLE_TRACKS_ADDED_TO_QUEUE, nltList.Count)));

                //Join audio
                if (firstJoin && botParams.allowJoinAudio && botParams.joinAudiosList.Count > 0)
                {
                    perServerSession[serverId].IsPlayingJoinAudio = true;
                    Random r = new Random();
                    int index = r.Next(0, botParams.joinAudiosList.Count - 1);

                    search = botParams.joinAudiosList[index];

                    var joinTuple = await LavalinkController.loadLavalinkTrack(node, search, false, 0);
                    List<NewLavalinkTrack> joinNewLavalinkTrack = joinTuple.Item1;

                    if (joinNewLavalinkTrack != null)
                    {
                        foreach (NewLavalinkTrack t in joinNewLavalinkTrack)
                        {
                            validJoinAudio = true;
                            perServerSession[serverId].Queue.AddFirst(t);
                        }
                        
                    }

                }


                if ((connection.CurrentState.CurrentTrack == null) && (!perServerSession[serverId].IsPlayingJoinAudio || validJoinAudio)) //Avoid other tracks "winning" the race against the join audio to be played
                {
                    await PlayNextTrack(connection, serverId, channelId, validJoinAudio);
                }

            }
            else
            {
                await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.BOT_ALREADY_IN_ANOTHER_VOICE_CHANNEL)));
            }

        }



        private async Task PlayNextTrack(LavalinkGuildConnection connection, ulong serverId, ulong channelId, bool joinAudio)
        {

            if (connection == null || !perServerSession.ContainsKey(serverId))
            {
                return;
            }

            DiscordChannel channel = connection.Guild.GetChannel(channelId);

            //afk check
            if (connection.Channel.Users.Count<=1)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.AFK_DISCONNECT)));
                DisconnectFromVoiceChannel(connection, channel, serverId);
                return;
            }

            if (perServerSession[serverId].IsPaused)
            {
                await connection.ResumeAsync();
                perServerSession[serverId].IsPaused = false;
            }


            NewLavalinkTrack nlt = perServerSession[serverId].Queue.First.Value;

            perServerSession[serverId].Queue.RemoveFirst();


            if (!joinAudio)
            {
                //speed up spotify songs load
                if (RegexUtils.matchSpotifySongUrl(nlt.Url))
                {
                    var tuple = await LavalinkController.loadLavalinkTrack(nlt.Node, nlt.Url, true, perServerSession[serverId].Queue.Count);
                    List<NewLavalinkTrack> nltList = tuple.Item1;
                    if (nltList.Count > 0)
                    {
                        nlt = nltList[0];
                    }
                }

                if (channel.Id!=0)
                {
                    if(nlt.ThumbnailUrl=="")
                        await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(nlt.Url, nlt.FinalTitle, messageManager.GetMessage(Message.PLAYING_ON, connection.Guild.Name), $"00:00:00 - {nlt.Duration}"));
                    else
                        await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(nlt.Url, nlt.FinalTitle, messageManager.GetMessage(Message.PLAYING_ON, connection.Guild.Name), $"00:00:00 - {nlt.Duration}", nlt.ThumbnailUrl));
                    NewLavalinkTrack nltCopy = new NewLavalinkTrack(nlt);
                    perServerSession[serverId].LastTrackPlayed = nltCopy;
                }        
            }

            perServerSession[serverId].IsPlayingJoinAudio = joinAudio;

            //If nlt.track (lavalinkTrack) is null then I should load the LavaLink track using the Youtube audio URL
            if (nlt.Track == null)
            {
                string realAudioURL = "";
                int maxRetries = 10;
                int retryCount = 0;
                bool success = false;

                while (retryCount < maxRetries && !success)
                {
                    try
                    {
                        realAudioURL = await YoutubeController.getAudioRealUrl(nlt.Url);
                        success = true;
                    }
                    catch (Exception e)
                    {
                        retryCount++;
                        if (retryCount >= maxRetries) 
                        {
                            if (perServerSession[serverId].Queue.Count > 0 || perServerSession[serverId].RadioMode || perServerSession[serverId].ArtistRadioMode)
                            {
                                Console.WriteLine($"Error getting real audio URL for {nlt.Url} after {maxRetries} retries. Skipping to next track.");
                                Console.WriteLine(e.ToString());
                                if (perServerSession[serverId].RadioMode || perServerSession[serverId].ArtistRadioMode)
                                    perServerSession[serverId].Queue.AddFirst(nlt);
                                await PlayNextTrack(connection, serverId, channelId, false);
                            }
                            else
                                DisconnectFromVoiceChannel(connection, channel, serverId);

                            return; 
                        }
                    }
                }

                if (!success)
                {
                    DisconnectFromVoiceChannel(connection, channel, serverId);
                    return;
                }

                nlt.Track=await LavalinkController.loadLavalinkTrack(realAudioURL, nlt.Node, LavalinkSearchType.Plain);
            }
            if(!string.IsNullOrEmpty(nlt.YoutubeVideoId) && (perServerSession[serverId].RadioMode || perServerSession[serverId].ArtistRadioMode) && !perServerSession[serverId].CurrentRadioHistory.ContainsKey(nlt.YoutubeVideoId))
            {
                perServerSession[serverId].CurrentRadioHistory.Add(nlt.YoutubeVideoId, true);
            }
            await connection.PlayAsync(nlt.Track);
            

        }

        private async Task OnPlaybackFinished(LavalinkGuildConnection connection, TrackFinishEventArgs e)
        {
            ulong serverId = connection.Guild.Id;
            perServerSession[serverId].IsPlayingJoinAudio = false;
            if (perServerSession[serverId].Queue.Count > 0)
            {
                await PlayNextTrack(connection, serverId, perServerSession[serverId].CallerTextChannelId, false);
            }
            else if (perServerSession[serverId].RadioMode || perServerSession[serverId].ArtistRadioMode)
            {
                NewLavalinkTrack result = null;
                string videoId;
                if (string.IsNullOrEmpty(perServerSession[serverId].LastTrackPlayed.YoutubeVideoId))
                    videoId = await YoutubeMusicController.getYoutubeMusicUrlFromQuery(perServerSession[serverId].LastTrackPlayed.FinalTitle);
                else
                    videoId = perServerSession[serverId].LastTrackPlayed.YoutubeVideoId;

                if (perServerSession[serverId].RadioMode)
                {
                    result = await LavalinkController.getNextRadioTrack(perServerSession[serverId].LastTrackPlayed.Node, videoId, perServerSession[serverId].CurrentRadioHistory); 
                }
                else if (perServerSession[serverId].ArtistRadioMode)
                {
                    result = await LavalinkController.getNextArtistRadioTrack(perServerSession[serverId].LastTrackPlayed.Node, videoId, perServerSession[serverId].ArtistRadioArtistId, perServerSession[serverId].CurrentRadioHistory);
                }


                if (result != null)
                {
                    perServerSession[serverId].Queue.AddLast(result);
                    perServerSession[serverId].CurrentRadioHistory.Add(result.YoutubeVideoId, true);
                    await PlayNextTrack(connection, serverId, perServerSession[serverId].CallerTextChannelId, false);
                }
            }
            else if (!perServerSession[serverId].HeavyOperationOngoing)
            {
                await connection.DisconnectAsync();
            }
        }



        [Command("pause")]
        public async Task PauseCommand(CommandContext ctx)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                var ll = ctx.Client.GetLavalink();

                if (!ll.ConnectedNodes.Any())
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                    return;
                }

                var node = ll.ConnectedNodes.Values.First();
                var connection = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
                ulong channelId = ctx.Channel.Id;
                DiscordChannel channel = connection.Guild.GetChannel(channelId);

                ulong serverId = ctx.Guild.Id;

                if (connection == null)
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                    return;
                }


                if (connection.CurrentState.CurrentTrack == null)
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NO_AUDIO_PLAYING)));
                    return;
                }

                perServerSession[serverId].IsPaused = true;

                await connection.PauseAsync();
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.PAUSED)));
            }
        }
        

        [Command("radio")]
        public async Task SwitchRadioCommand(CommandContext ctx)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                var ll = ctx.Client.GetLavalink();

                if (!ll.ConnectedNodes.Any())
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                    return;
                }

                var node = ll.ConnectedNodes.Values.First();
                var connection = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
                ulong channelId = ctx.Channel.Id;
                DiscordChannel channel = connection.Guild.GetChannel(channelId);

                ulong serverId = ctx.Guild.Id;

                if (connection == null)
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                    return;
                }

                if (!isSpotifyEnabled())
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NO_SPOTIFY_CREDENTIALS)));
                    return;
                }

                if (perServerSession[serverId].RadioMode)
                {
                    perServerSession[serverId].CurrentRadioHistory.Clear();
                    perServerSession[serverId].RadioMode = false;
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.RADIO_MODE_DISABLED)));
                    return;
                }

                if (connection.CurrentState.CurrentTrack == null)
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NO_AUDIO_PLAYING)));
                    return;
                }

                if (perServerSession[serverId].Queue.Count > 0)
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.QUEUE_MUST_BE_EMPTY_FOR_RADIO_MODE)));
                    return;
                }

                if (perServerSession[serverId].ArtistRadioMode)
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.ARTIST_RADIO_MODE_ENABLED)));
                    return;
                }

                perServerSession[serverId].RadioMode = true;
                perServerSession[serverId].CurrentRadioHistory = new Dictionary<string, bool>();
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.RADIO_MODE_ENABLED)));
            }
        }



        [Command("resume")]
        public async Task ResumeCommand(CommandContext ctx)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                var ll = ctx.Client.GetLavalink();

                if (!ll.ConnectedNodes.Any())
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                    return;
                }

                var node = ll.ConnectedNodes.Values.First();
                var connection = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
                ulong channelId = ctx.Channel.Id;
                DiscordChannel channel = connection.Guild.GetChannel(channelId);

                ulong serverId = ctx.Guild.Id;

                if (connection == null)
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                    return;
                }

                string trackTitle = connection.CurrentState.CurrentTrack.Title;

                if (perServerSession[serverId].IsPaused)
                {
                    await connection.ResumeAsync();
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.RESUMED)));
                }
                perServerSession[serverId].IsPaused = false;
            }
        }


        private bool AreBotAndCallerInTheSameChannel(CommandContext ctx)
        {
            if (ctx.Member.VoiceState == null)
            {
                return false;
            }
            else
            {
                var ll = ctx.Client.GetLavalink();
                var node = ll.ConnectedNodes.Values.First();
                var connection = node.GetGuildConnection(ctx.Member.VoiceState.Channel.Guild);
                return connection != null && connection.Channel == ctx.Member.VoiceState.Channel;
            }
        }

        [Command("queue")]
        public async Task NewQueueCommand(CommandContext ctx, [RemainingText] string pageStr)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                int page = 1;
                try
                {
                    page = int.Parse(pageStr);
                }
                catch(Exception e)
                {
                }

                ushort amountPerPage = 50;

                var ll = ctx.Client.GetLavalink();

                if (!ll.ConnectedNodes.Any())
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                    return;
                }

                var node = ll.ConnectedNodes.Values.First();
                var connection = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
                ulong channelId = ctx.Channel.Id;
                DiscordChannel channel = connection.Guild.GetChannel(channelId);
                ulong serverId = ctx.Guild.Id;

                if (perServerSession[serverId].HeavyOperationOngoing)
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.HEAVY_OPERATION_ONGOING)));
                    return;
                }

                if (perServerSession[serverId].Queue.Count <= 0)
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NO_ELEMENTS_IN_QUEUE)));
                    return;
                }

                int pages = (int)Math.Ceiling((double)perServerSession[serverId].Queue.Count / amountPerPage);
                if (page > pages)
                {
                    page = pages;
                }
                int x = (page - 1) * amountPerPage;
                int count = perServerSession[serverId].Queue.Count - x;
                if (count > amountPerPage)
                    count = amountPerPage;
                string queueInfo = "";
                int index = (page-1) * amountPerPage;
                if (index == 0)
                    index = 1;
                queueInfo += $"📄 {page}/{pages}\n\n";
                foreach (NewLavalinkTrack t in perServerSession[serverId].Queue.Skip(x).Take(count))
                {
                    queueInfo += $"{index}: {t.FinalTitle}\n";
                    index++;
                }
                var queueEmbed = CustomEmbedBuilder.CreateEmbed(queueInfo);
                await channel.SendMessageAsync(queueEmbed);

            }
        }

        [Command("remove")]
        public async Task RemoveCommand(CommandContext ctx, int position)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                ulong serverId = ctx.Guild.Id;

                if (perServerSession[serverId].HeavyOperationOngoing)
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.HEAVY_OPERATION_ONGOING)));
                    return;
                }

                var lava = ctx.Client.GetLavalink();
                var node = lava.ConnectedNodes.Values.First();
                var connection = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
                ulong channelId = ctx.Channel.Id;
                DiscordChannel channel = connection.Guild.GetChannel(channelId);     
                if (position <= perServerSession[serverId].Queue.Count)
                {
                    position--;
                    int index = 0;
                    foreach (NewLavalinkTrack t in perServerSession[serverId].Queue)
                    {
                        if (position == index)
                        {
                            perServerSession[serverId].Queue.Remove(t);

                            if (channel != null)
                            {
                                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.DELETED, t.FinalTitle)));
                            }

                            break;
                        }
                        index++;
                    }
                }
                else
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.OUT_OF_RANGE_IN_QUEUE)));
                }
            }
        }

        [Command("clear")]
        public async Task ClearCommand(CommandContext ctx)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                ulong serverId = ctx.Guild.Id;

                if (perServerSession[serverId].HeavyOperationOngoing)
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.HEAVY_OPERATION_ONGOING)));
                    return;
                }

                var lava = ctx.Client.GetLavalink();
                var node = lava.ConnectedNodes.Values.First();
                var connection = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
                ulong channelId = ctx.Channel.Id;
                DiscordChannel channel = connection.Guild.GetChannel(channelId);

                perServerSession[serverId].Queue.Clear();
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.CLEARED)));
            }
        }

        [Command("skip")]
        public async Task SkipCommand(CommandContext ctx)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                var ll = ctx.Client.GetLavalink();

                if (!ll.ConnectedNodes.Any())
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                    return;
                }

                var node = ll.ConnectedNodes.Values.First();
                var connection = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
                ulong serverId = ctx.Guild.Id;
                ulong channelId = ctx.Channel.Id;

                if (perServerSession[serverId].HeavyOperationOngoing)
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.HEAVY_OPERATION_ONGOING)));
                    return;
                }

                DiscordChannel channel = connection.Guild.GetChannel(channelId);

                if (connection == null)
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                    return;
                }

                if (connection.CurrentState.CurrentTrack != null)
                {
                    await connection.StopAsync();
                    if (!perServerSession[serverId].IsPlayingJoinAudio)
                        await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.SKIPPED)));
                }


            }
        }

        [Command("stop")]
        public async Task StopCommand(CommandContext ctx)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                var ll = ctx.Client.GetLavalink();

                if (!ll.ConnectedNodes.Any())
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                    return;
                }

                var node = ll.ConnectedNodes.Values.First();
                var connection = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
                ulong serverId = ctx.Guild.Id;

                if (perServerSession[serverId].HeavyOperationOngoing)
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.HEAVY_OPERATION_ONGOING)));
                    return;
                }

                ulong channelId = ctx.Channel.Id;
                DiscordChannel channel = connection.Guild.GetChannel(channelId);
                DisconnectFromVoiceChannel(connection, channel, serverId);
            }
        }

        [Command("shuffle")]
        public async Task ShuffleCommand(CommandContext ctx)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                ulong serverId = ctx.Guild.Id;

                if (perServerSession[serverId].HeavyOperationOngoing)
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.HEAVY_OPERATION_ONGOING)));
                    return;
                }

                var lava = ctx.Client.GetLavalink();
                var node = lava.ConnectedNodes.Values.First();
                var connection = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
                ulong channelId = ctx.Channel.Id;
                DiscordChannel channel = connection.Guild.GetChannel(channelId);

                if (perServerSession[serverId].Queue.Count > 0)
                {
                    Random r = new Random();
                    NewLavalinkTrack[] newLavalinkTracks = perServerSession[serverId].Queue.ToArray();

                    r.Shuffle(newLavalinkTracks);

                    perServerSession[serverId].Queue.Clear();

                    foreach (NewLavalinkTrack t in newLavalinkTracks)
                    {
                        perServerSession[serverId].Queue.AddLast(t);
                    }

                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.SHUFFLED)));
                }
                else
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NO_ELEMENTS_IN_QUEUE)));
                }
            }
        }

        private async void DisconnectFromVoiceChannel(LavalinkGuildConnection connection, DiscordChannel channel, ulong serverId)
        {
            await connection.DisconnectAsync();

            perServerSession[serverId].Queue.Clear();
            perServerSession[serverId].RadioMode = false;
            perServerSession[serverId].ArtistRadioMode = false;
            perServerSession.Remove(serverId);

            await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.STOPPED)));
        }

    }

}
