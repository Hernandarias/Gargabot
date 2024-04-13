using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using DSharpPlus.Lavalink.EventArgs;
using Gargabot.AudioSessions;
using Gargabot.Utils.DiscordUtils;
using Gargabot.Utils.LavalinkUtilities;
using Gargabot.Utils.Youtube;
using Newtonsoft.Json;

namespace Gargabot.Commands
{
    public class LavalinkCommandModule : UniversalCommandModule
    {
        private Dictionary<ulong, LavalinkVoiceSession> perServerSession = new Dictionary<ulong, LavalinkVoiceSession>();

        [Command("play")]
        public async Task Play(CommandContext ctx, [RemainingText] string search)
        {
            if (ctx.Member.VoiceState == null)
            {
                await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("NOT_IN_A_VOICE_CHANNEL")));
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
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("GET_LAVALINK_CONNECTION_ERROR")));
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
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("HEAVY_OPERATION_ONGOING")));
                    return;
                }
                else if (perServerSession[serverId].Queue.Count+1 > botParams.perServerQueueLimit)
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("QUEUE_LIMIT_REACHED")));
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

                perServerSession[serverId].HeavyOperationOngoing = true;

                var tuple = await LavalinkUtils.loadLavalinkTrack(node, search, isMusic, perServerSession[serverId].Queue.Count);

                perServerSession[serverId].HeavyOperationOngoing = false;

                if (tuple.Item2)
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("QUEUE_LIMIT_REACHED")));
                    return;
                }

                List<NewLavalinkTrack> nltList = tuple.Item1;

                if (nltList.Count==0)
                {

                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("NO_TRACKS_FOUND_FOR_SEARCH", search)));
                    if (perServerSession[serverId].Joined && connection.CurrentState.CurrentTrack == null && perServerSession[serverId].Queue.Count <= 0)
                        await StopCommand(ctx);
                    return;
                }   

                foreach (NewLavalinkTrack nlt in nltList)
                {

                    perServerSession[serverId].Queue.AddLast(nlt);
                    if(nltList.Count==1)
                        await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(nlt.Url, nlt.FinalTitle, messageManager.GetMessage("ADDED_TO_QUEUE_IN_POSITION", perServerSession[serverId].Queue.Count)));
                }

                if (nltList.Count > 1)
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("MULTIPLE_TRACKS_ADDED_TO_QUEUE", nltList.Count)));

                //Join audio
                if (firstJoin && botParams.allowJoinAudio && botParams.joinAudiosList.Count > 0)
                {
                    perServerSession[serverId].IsPlayingJoinAudio = true;
                    Random r = new Random();
                    int index = r.Next(0, botParams.joinAudiosList.Count - 1);

                    search = botParams.joinAudiosList[index];

                    var joinTuple = await LavalinkUtils.loadLavalinkTrack(node, search, false, 0);
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
                await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("BOT_ALREADY_IN_ANOTHER_VOICE_CHANNEL")));
            }

        }



        private async Task PlayNextTrack(LavalinkGuildConnection connection, ulong serverId, ulong channelId, bool joinAudio)
        {

            if (connection == null)
            {
                return;
            }

            if (perServerSession[serverId].IsPaused)
            {
                await connection.ResumeAsync();
                perServerSession[serverId].IsPaused = false;
            }


            NewLavalinkTrack nlt = perServerSession[serverId].Queue.First.Value;

            perServerSession[serverId].Queue.RemoveFirst();

            DiscordChannel channel = connection.Guild.GetChannel(channelId);

            if (!joinAudio)
            {
                if (channel != null)
                {
                    if(nlt.ThumbnailUrl=="")
                        await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(nlt.Url, nlt.FinalTitle, messageManager.GetMessage("PLAYING_ON", connection.Guild.Name), $"00:00:00 - {nlt.Duration}"));
                    else
                        await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(nlt.Url, nlt.FinalTitle, messageManager.GetMessage("PLAYING_ON", connection.Guild.Name), $"00:00:00 - {nlt.Duration}", nlt.ThumbnailUrl));
                }        
            }

            perServerSession[serverId].IsPlayingJoinAudio = joinAudio;


            if (nlt.Track == null)
            {
                //If nlt.track (lavalinkTrack) is null then I should load the LavaLink track using the Youtube audio URL
                string realAudioURL = await YoutubeUtils.getAudioRealUrl(nlt.Url);
                nlt.Track=await LavalinkUtils.loadLavalinkTrack(realAudioURL, nlt.Node, LavalinkSearchType.Plain);
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
            else
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
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("GET_LAVALINK_CONNECTION_ERROR")));
                    return;
                }

                var node = ll.ConnectedNodes.Values.First();
                var connection = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
                ulong channelId = ctx.Channel.Id;
                DiscordChannel channel = connection.Guild.GetChannel(channelId);

                ulong serverId = ctx.Guild.Id;

                if (connection == null)
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("GET_LAVALINK_CONNECTION_ERROR")));
                    return;
                }


                if (connection.CurrentState.CurrentTrack == null)
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("NO_AUDIO_PLAYING")));
                    return;
                }

                perServerSession[serverId].IsPaused = true;

                await connection.PauseAsync();
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("PAUSED")));
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
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("GET_LAVALINK_CONNECTION_ERROR")));
                    return;
                }

                var node = ll.ConnectedNodes.Values.First();
                var connection = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
                ulong channelId = ctx.Channel.Id;
                DiscordChannel channel = connection.Guild.GetChannel(channelId);

                ulong serverId = ctx.Guild.Id;

                if (connection == null)
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("GET_LAVALINK_CONNECTION_ERROR")));
                    return;
                }

                string trackTitle = connection.CurrentState.CurrentTrack.Title;

                if (perServerSession[serverId].IsPaused)
                {
                    await connection.ResumeAsync();
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("RESUMED")));
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
        public async Task QueueCommand(CommandContext ctx)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                var ll = ctx.Client.GetLavalink();

                if (!ll.ConnectedNodes.Any())
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("GET_LAVALINK_CONNECTION_ERROR")));
                    return;
                }

                var node = ll.ConnectedNodes.Values.First();
                var connection = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
                ulong channelId = ctx.Channel.Id;
                DiscordChannel channel = connection.Guild.GetChannel(channelId);
                ulong serverId = ctx.Guild.Id;

                if (perServerSession[serverId].HeavyOperationOngoing)
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("HEAVY_OPERATION_ONGOING")));
                    return;
                }

                if (perServerSession[serverId].Queue.Count > 0)
                {
                    int index = 1;
                    string queueInfo = "";
                    foreach (NewLavalinkTrack t in perServerSession[serverId].Queue)
                    {
                        queueInfo += $"{index}: {t.FinalTitle}\n";
                        index++;
                    }
                    var queueEmbed = CustomEmbedBuilder.CreateEmbed(queueInfo);
                    await channel.SendMessageAsync(queueEmbed);
                }
                else
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("NO_ELEMENTS_IN_QUEUE")));
                }
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
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("HEAVY_OPERATION_ONGOING")));
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
                                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("DELETED", t.FinalTitle)));
                            }

                            break;
                        }
                        index++;
                    }
                }
                else
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("OUT_OF_RANGE_IN_QUEUE")));
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
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("HEAVY_OPERATION_ONGOING")));
                    return;
                }

                var lava = ctx.Client.GetLavalink();
                var node = lava.ConnectedNodes.Values.First();
                var connection = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
                ulong channelId = ctx.Channel.Id;
                DiscordChannel channel = connection.Guild.GetChannel(channelId);

                perServerSession[serverId].Queue.Clear();
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("CLEARED")));
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
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("GET_LAVALINK_CONNECTION_ERROR")));
                    return;
                }

                var node = ll.ConnectedNodes.Values.First();
                var connection = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
                ulong serverId = ctx.Guild.Id;
                ulong channelId = ctx.Channel.Id;

                if (perServerSession[serverId].HeavyOperationOngoing)
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("HEAVY_OPERATION_ONGOING")));
                    return;
                }

                DiscordChannel channel = connection.Guild.GetChannel(channelId);

                if (connection == null)
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("GET_LAVALINK_CONNECTION_ERROR")));
                    return;
                }

                if (connection.CurrentState.CurrentTrack != null)
                {
                    await connection.StopAsync();
                    if (!perServerSession[serverId].IsPlayingJoinAudio)
                        await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("SKIPPED")));
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
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("GET_LAVALINK_CONNECTION_ERROR")));
                    return;
                }

                var node = ll.ConnectedNodes.Values.First();
                var connection = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
                ulong serverId = ctx.Guild.Id;

                if (perServerSession[serverId].HeavyOperationOngoing)
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("HEAVY_OPERATION_ONGOING")));
                    return;
                }

                await connection.DisconnectAsync();
                ulong channelId = ctx.Channel.Id;
                DiscordChannel channel = connection.Guild.GetChannel(channelId);

                perServerSession[serverId].Queue.Clear();
                perServerSession.Remove(serverId);

                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("STOPPED")));
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
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("HEAVY_OPERATION_ONGOING")));
                    return;
                }

                var lava = ctx.Client.GetLavalink();
                var node = lava.ConnectedNodes.Values.First();
                var connection = node.GetGuildConnection(ctx.Member.VoiceState.Guild);
                ulong channelId = ctx.Channel.Id;
                DiscordChannel channel = connection.Guild.GetChannel(channelId);

                //This is an O(3n) operation
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

                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("SHUFFLED")));
                }
                else
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("NO_ELEMENTS_IN_QUEUE")));
                }
            }
        }

    }

}
