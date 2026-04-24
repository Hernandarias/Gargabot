using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Gargabot.AudioSessions;
using Gargabot.Messages;
using Gargabot.Utils;
using Gargabot.Utils.DiscordUtils;
using Gargabot.Utils.FMRadioUtils;
using Gargabot.Utils.LavalinkUtilities;
using Gargabot.Utils.Youtube;
using Lavalink4NET.Clients;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Gargabot.Commands
{
    public class LavalinkCommandModule : UniversalCommandModule
    {
        private sealed class FmRadioSelectionSession
        {
            public ulong UserId { get; set; }
            public List<RadioBrowserStation> Results { get; set; } = new();
        }

        private readonly Dictionary<string, FmRadioSelectionSession> fmRadioSelections = new();
        private readonly Dictionary<ulong, LavalinkVoiceSession> perServerSession = new();
        private readonly Dictionary<ulong, QueuedLavalinkPlayer> activePlayers = new();
        private readonly Dictionary<ulong, Task> playbackWatchers = new();

        private const int DefaultVolume = 100;
        private const int MinVolume = 0;
        private const int MaxVolume = 200;
        private const int VolumeStep = 10;

        public LavalinkCommandModule()
        {
            Program.OnPauseButtonPressed += HandlePauseButton;
            Program.OnSkipButtonPressed += HandleSkipButton;
            Program.OnStopButtonPressed += HandleStopButton;
            Program.OnLoopButtonPressed += HandleLoopButton;
            Program.OnQueueButtonPressed += HandleQueueButton;
            Program.OnShuffleButtonPressed += HandleShuffleButton;
            Program.OnVolumeDownButtonPressed += HandleVolumeDownButton;
            Program.OnVolumeUpButtonPressed += HandleVolumeUpButton;
            Program.OnFmRadioSelectPressed += HandleFmRadioSelect;
        }

        [Command("play")]
        public async Task Play(CommandContext ctx, [RemainingText] string search)
        {
            await PlayInternal(ctx, search, false, false);
        }

        [Command("playnext")]
        public async Task PlayNext(CommandContext ctx, [RemainingText] string search)
        {
            await PlayInternal(ctx, search, true, false);
        }

        private async Task PlayInternal(CommandContext ctx, string search, bool next, bool fm)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return;
            }

            if (ctx.Member?.VoiceState is null)
            {
                await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NOT_IN_A_VOICE_CHANNEL)));
                return;
            }

            ulong serverId = ctx.Guild.Id;
            ulong channelId = ctx.Channel.Id;
            DiscordChannel channel = ctx.Channel;

            var player = GetPlayer(serverId);

            if (!AreBotAndCallerInTheSameChannel(ctx) && player is not null)
            {
                await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.BOT_ALREADY_IN_ANOTHER_VOICE_CHANNEL)));
                return;
            }

            if (!perServerSession.ContainsKey(serverId))
            {
                perServerSession[serverId] = new LavalinkVoiceSession
                {
                    Joined = false,
                    CallerTextChannelId = channelId,
                    HeavyOperationOngoing = false,
                    Volume = DefaultVolume
                };
            }
            else if (perServerSession[serverId].HeavyOperationOngoing)
            {
                await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.HEAVY_OPERATION_ONGOING)));
                return;
            }
            else if (perServerSession[serverId].Queue.Count + 1 > botParams.perServerQueueLimit)
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

            if (player is null)
            {
                player = await RetrievePlayerAsync(ctx, connectToVoiceChannel: true);

                if (player is null)
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                    return;
                }

                firstJoin = true;
                perServerSession[serverId].Joined = true;
            }

            bool isMusic = false;

            if (search.StartsWith("[!music]"))
            {
                search = search.Remove(0, 8);
                isMusic = true;
            }
            else if (search.StartsWith("[!artistradio]") && firstJoin)
            {
                if (!isSpotifyEnabled())
                {
                    await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NO_SPOTIFY_CREDENTIALS)));
                    return;
                }

                search = search.Remove(0, 14);
                Tuple<string, string> artistIdAndTrack = await LavalinkController.getArtistIdAndTrackFromName(search);

                if (artistIdAndTrack.Item1 is not null && artistIdAndTrack.Item2 is not null)
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

            Tuple<List<NewLavalinkTrack>, bool> tuple;
            try
            {
                tuple = await LavalinkController.loadLavalinkTrack(Program.AudioService, search, isMusic, perServerSession[serverId].Queue.Count);
            }
            finally
            {
                perServerSession[serverId].HeavyOperationOngoing = false;
            }

            if (tuple.Item2)
            {
                await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.QUEUE_LIMIT_REACHED)));
                return;
            }

            List<NewLavalinkTrack> nltList = tuple.Item1;

            if (nltList.Count == 0)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NO_TRACKS_FOUND_FOR_SEARCH, search)));

                if (perServerSession[serverId].Joined && player.CurrentTrack is null && perServerSession[serverId].Queue.Count <= 0)
                {
                    await StopCommand(ctx);
                }

                return;
            }

            if (next)
            {
                nltList.Reverse();

                foreach (NewLavalinkTrack nlt in nltList)
                {
                    perServerSession[serverId].Queue.AddFirst(nlt);
                    if (fm)
                        break;
                }
            }
            else
            {
                foreach (NewLavalinkTrack nlt in nltList)
                {
                    perServerSession[serverId].Queue.AddLast(nlt);
                    if (fm)
                        break;
                }
            }

            if (tuple.Item1.Count == 1 || fm)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(tuple.Item1[0].Url, tuple.Item1[0].FinalTitle, messageManager.GetMessage(Message.ADDED_TO_QUEUE_IN_POSITION, 1)));
            }
            else
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.MULTIPLE_TRACKS_ADDED_TO_QUEUE, tuple.Item1.Count)));
            }

            if (firstJoin && botParams.allowJoinAudio && botParams.joinAudiosList!.Count > 0)
            {
                Random r = new();
                int index = r.Next(0, botParams.joinAudiosList.Count);
                string joinSearch = botParams.joinAudiosList[index];

                var joinTuple = await LavalinkController.loadLavalinkTrack(Program.AudioService, joinSearch, false, 0);

                foreach (NewLavalinkTrack t in joinTuple.Item1)
                {
                    validJoinAudio = true;
                    perServerSession[serverId].Queue.AddFirst(t);
                }
            }

            if (player.CurrentTrack is null && !perServerSession[serverId].IsStartingPlayback)
            {
                await PlayNextTrack(player, serverId, channelId, validJoinAudio);
            }
        }

        private async Task<QueuedLavalinkPlayer?> RetrievePlayerAsync(CommandContext ctx, bool connectToVoiceChannel)
        {
            ulong guildId = ctx.Guild.Id;
            ulong? memberVoiceChannel = ctx.Member?.VoiceState?.Channel?.Id;

            PlayerChannelBehavior channelBehavior;

            if (connectToVoiceChannel)
            {
                channelBehavior = PlayerChannelBehavior.Join;
            }
            else
            {
                channelBehavior = PlayerChannelBehavior.None;
            }

            var retrieveOptions = new PlayerRetrieveOptions(ChannelBehavior: channelBehavior, VoiceStateBehavior: MemberVoiceStateBehavior.RequireSame);

            int initialVolume = DefaultVolume;
            if (perServerSession.ContainsKey(guildId))
            {
                initialVolume = Math.Clamp(perServerSession[guildId].Volume, MinVolume, MaxVolume);
            }

            var playerOptions = new QueuedLavalinkPlayerOptions
            {
                InitialVolume = initialVolume / 100f
            };

            var result = await Program.AudioService.Players.RetrieveAsync(guildId,memberVoiceChannel, PlayerFactory.Queued, Options.Create(playerOptions), retrieveOptions: retrieveOptions).ConfigureAwait(false);

            if (!result.IsSuccess || result.Player is null)
            {
                return null;
            }

            activePlayers[guildId] = result.Player;
            return result.Player;
        }

        private QueuedLavalinkPlayer? GetPlayer(ulong guildId)
        {
            if (activePlayers.TryGetValue(guildId, out var player))
            {
                return player;
            }
            else
            {
                return null;
            }
        }

        private async Task PlayNextTrack(QueuedLavalinkPlayer player, ulong serverId, ulong channelId, bool joinAudio)
        {
            if (!perServerSession.ContainsKey(serverId) ||
                perServerSession[serverId].Queue.Count == 0)
            {
                return;
            }

            if (perServerSession[serverId].IsStartingPlayback)
            {
                return;
            }

            perServerSession[serverId].IsStartingPlayback = true;

            try
            {
                var guild = Program.Discord.Guilds[serverId];
                DiscordChannel channel = guild.GetChannel(channelId);
                DiscordChannel voiceChannel = guild.GetChannel(player.VoiceChannelId);

                if (voiceChannel.Users.Count <= 1)
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.AFK_DISCONNECT)));
                    await DisconnectFromVoiceChannel(player, channel, serverId);
                    return;
                }

                if (perServerSession[serverId].IsPaused)
                {
                    await player.ResumeAsync();
                    perServerSession[serverId].IsPaused = false;
                }

                if (player.CurrentTrack is not null)
                {
                    return;
                }

                NewLavalinkTrack nlt = perServerSession[serverId].Queue.First!.Value;

                if (!joinAudio)
                {
                    if (RegexUtils.matchSpotifySongUrl(nlt.Url))
                    {
                        var tuple = await LavalinkController.loadLavalinkTrack(Program.AudioService, nlt.Url, true, perServerSession[serverId].Queue.Count);

                        if (tuple.Item1.Count > 0)
                        {
                            nlt = tuple.Item1[0];
                        }
                    }

                    DiscordEmbedBuilder embed;
                    string initialTimestamp = "";
                    if (!string.IsNullOrEmpty(nlt.Duration))
                        initialTimestamp = "00:00:00";
                    if (string.IsNullOrEmpty(nlt.ThumbnailUrl))
                        embed = CustomEmbedBuilder.CreateEmbed(nlt.Url, nlt.FinalTitle, messageManager.GetMessage(Message.PLAYING_ON, guild.Name), $"{initialTimestamp} - {nlt.Duration}");
                    else
                        embed = CustomEmbedBuilder.CreateEmbed(nlt.Url, nlt.FinalTitle, messageManager.GetMessage(Message.PLAYING_ON, guild.Name), $"{initialTimestamp} - {nlt.Duration}", nlt.ThumbnailUrl);

                    var messageWithButtons = CustomEmbedBuilder.CreatePlayEmbed(embed);
                    await channel.SendMessageAsync(messageWithButtons);

                    perServerSession[serverId].LastTrackPlayed = new NewLavalinkTrack(nlt);
                }

                perServerSession[serverId].IsPlayingJoinAudio = joinAudio;

                if (nlt.Track is null)
                {
                    string realAudioURL = "";
                    const int maxRetries = 15;
                    int retryCount = 0;
                    bool success = false;

                    while (retryCount < maxRetries && !success)
                    {
                        try
                        {
                            realAudioURL = await YoutubeController.getAudioRealUrl(nlt.Url);
                            success = true;
                        }
                        catch (Exception ex)
                        {
                            retryCount++;

                            if (retryCount >= maxRetries)
                            {
                                if (perServerSession[serverId].Queue.Count > 1 || perServerSession[serverId].RadioMode || perServerSession[serverId].ArtistRadioMode)
                                {
                                    Console.WriteLine($"Error getting real audio URL for {nlt.Url} after {maxRetries} retries. Skipping to next track.");
                                    Console.WriteLine(ex);

                                    perServerSession[serverId].Queue.RemoveFirst();

                                    await Task.Delay(500);
                                    await PlayNextTrack(player, serverId, channelId, false);
                                }
                                else
                                {
                                    await DisconnectFromVoiceChannel(player, channel, serverId);
                                }

                                return;
                            }
                        }
                    }

                    if (!success)
                    {
                        return;
                    }

                    nlt.Track = await LavalinkController.loadLavalinkTrack(realAudioURL, Program.AudioService, TrackSearchMode.None);
                }

                if (nlt.Track is null)
                {
                    if (perServerSession[serverId].Queue.Count > 1 || perServerSession[serverId].RadioMode || perServerSession[serverId].ArtistRadioMode)
                    {
                        perServerSession[serverId].Queue.RemoveFirst();
                        await PlayNextTrack(player, serverId, channelId, false);
                    }
                    else
                    {
                        await DisconnectFromVoiceChannel(player, channel, serverId);
                    }

                    return;
                }

                if (!string.IsNullOrEmpty(nlt.YoutubeVideoId) && (perServerSession[serverId].RadioMode || perServerSession[serverId].ArtistRadioMode) && !perServerSession[serverId].CurrentRadioHistory!.ContainsKey(nlt.YoutubeVideoId))
                {
                    perServerSession[serverId].CurrentRadioHistory!.Add(nlt.YoutubeVideoId, true);
                }

                await player.PlayAsync(nlt.Track);

                // recién ahora lo sacamos de la cola
                if (perServerSession.ContainsKey(serverId) &&
                    perServerSession[serverId].Queue.Count > 0)
                {
                    perServerSession[serverId].Queue.RemoveFirst();
                }

                EnsurePlaybackWatcher(player, serverId, channelId);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error playing track: " + JsonSerializer.Serialize(perServerSession[serverId].Queue.FirstOrDefault()) + ex);
            }
            finally
            {
                if (perServerSession.ContainsKey(serverId))
                {
                    perServerSession[serverId].IsStartingPlayback = false;
                }
            }
        }

        private void EnsurePlaybackWatcher(QueuedLavalinkPlayer player, ulong serverId, ulong channelId)
        {
            if (playbackWatchers.TryGetValue(serverId, out var currentTask) && !currentTask.IsCompleted)
            {
                return;
            }

            playbackWatchers[serverId] = Task.Run(() => WatchPlaybackAsync(player, serverId, channelId));
        }

        private async Task WatchPlaybackAsync(QueuedLavalinkPlayer player, ulong serverId, ulong channelId)
        {
            try
            {
                await Task.Delay(500);

                while (perServerSession.ContainsKey(serverId) && activePlayers.TryGetValue(serverId, out var currentPlayer) && ReferenceEquals(currentPlayer, player))
                {
                    while (perServerSession.ContainsKey(serverId) && activePlayers.TryGetValue(serverId, out currentPlayer) && ReferenceEquals(currentPlayer, player) && (player.CurrentTrack is not null || perServerSession[serverId].IsPaused))
                    {
                        await Task.Delay(500);
                    }

                    if (!perServerSession.ContainsKey(serverId))
                    {
                        return;
                    }

                    if (!activePlayers.TryGetValue(serverId, out currentPlayer) || !ReferenceEquals(currentPlayer, player))
                    {
                        return;
                    }

                    if (perServerSession[serverId].HeavyOperationOngoing)
                    {
                        await Task.Delay(250);
                        continue;
                    }

                    if (player.CurrentTrack is null)
                    {
                        await OnPlaybackFinished(player, serverId, channelId);
                    }

                    await Task.Delay(250);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Playback watcher error on guild {serverId}: {ex}");
            }
            finally
            {
                playbackWatchers.Remove(serverId);
            }
        }

        private async Task OnPlaybackFinished(QueuedLavalinkPlayer player, ulong serverId, ulong channelId)
        {
            if (!perServerSession.ContainsKey(serverId))
            {
                return;
            }

            if (perServerSession[serverId].IsStartingPlayback)
            {
                return;
            }

            perServerSession[serverId].IsPlayingJoinAudio = false;

            if (perServerSession[serverId].Loop)
            {
                perServerSession[serverId].Queue.AddFirst(new NewLavalinkTrack(perServerSession[serverId].LastTrackPlayed!));

                await PlayNextTrack(player, serverId, perServerSession[serverId].CallerTextChannelId, false);
            }
            else if (perServerSession[serverId].Queue.Count > 0)
            {
                await PlayNextTrack(player, serverId, perServerSession[serverId].CallerTextChannelId, false);
            }
            else if (perServerSession[serverId].RadioMode || perServerSession[serverId].ArtistRadioMode)
            {
                NewLavalinkTrack result = new();

                string videoId;
                if (string.IsNullOrEmpty(perServerSession[serverId].LastTrackPlayed!.YoutubeVideoId))
                {
                    videoId = await YoutubeMusicController.getYoutubeMusicUrlFromQuery(perServerSession[serverId].LastTrackPlayed!.FinalTitle);
                }
                else
                {
                    videoId = perServerSession[serverId].LastTrackPlayed!.YoutubeVideoId;
                }

                if (perServerSession[serverId].RadioMode)
                {
                    result = await LavalinkController.getNextRadioTrack(Program.AudioService, videoId, perServerSession[serverId].CurrentRadioHistory!);
                }
                else if (perServerSession[serverId].ArtistRadioMode)
                {
                    result = await LavalinkController.getNextArtistRadioTrack(Program.AudioService, videoId, perServerSession[serverId].ArtistRadioArtistId!, perServerSession[serverId].CurrentRadioHistory!);
                }

                if (result is not null)
                {
                    perServerSession[serverId].Queue.AddLast(result);

                    if (!string.IsNullOrEmpty(result.YoutubeVideoId) && !perServerSession[serverId].CurrentRadioHistory!.ContainsKey(result.YoutubeVideoId))
                    {
                        perServerSession[serverId].CurrentRadioHistory!.Add(result.YoutubeVideoId, true);
                    }

                    await PlayNextTrack(player, serverId, perServerSession[serverId].CallerTextChannelId, false);
                }
            }
            else if (!perServerSession[serverId].HeavyOperationOngoing)
            {
                await player.DisconnectAsync();
                CleanupSession(serverId);
            }
        }

        [Command("pause")]
        public async Task PauseCommand(CommandContext ctx)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                await Pause(ctx.Client, ctx.Guild.Id, ctx.Channel.Id);
            }
        }

        private async Task Pause(DiscordClient client, ulong serverId, ulong channelId)
        {
            var player = GetPlayer(serverId);
            var channel = await client.GetChannelAsync(channelId);

            if (player is null)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                return;
            }

            if (player.CurrentTrack is null)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NO_AUDIO_PLAYING)));
                return;
            }

            perServerSession[serverId].IsPaused = true;
            await player.PauseAsync();

            await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.PAUSED)));
        }

        [Command("radio")]
        public async Task SwitchRadioCommand(CommandContext ctx)
        {
            if (!AreBotAndCallerInTheSameChannel(ctx))
            {
                return;
            }

            ulong serverId = ctx.Guild.Id;
            DiscordChannel channel = ctx.Channel;
            var player = GetPlayer(serverId);

            if (player is null)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                return;
            }

            if (!isSpotifyEnabled())
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NO_SPOTIFY_CREDENTIALS)));
                return;
            }

            if (perServerSession[serverId].Loop)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.LOOP_IS_ENABLED)));
                return;
            }

            if (perServerSession[serverId].RadioMode)
            {
                perServerSession[serverId].CurrentRadioHistory!.Clear();
                perServerSession[serverId].RadioMode = false;

                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.RADIO_MODE_DISABLED)));
                return;
            }

            if (player.CurrentTrack is null)
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
            perServerSession[serverId].CurrentRadioHistory = new Dictionary<string, bool>
            {
                { perServerSession[serverId].LastTrackPlayed!.YoutubeVideoId, true },
            };

            await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.RADIO_MODE_ENABLED)));
        }

        [Command("resume")]
        public async Task ResumeCommand(CommandContext ctx)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                await Resume(ctx.Client, ctx.Guild.Id, ctx.Channel.Id);
            }
        }

        private async Task Resume(DiscordClient client, ulong serverId, ulong channelId)
        {
            var player = GetPlayer(serverId);
            var channel = await client.GetChannelAsync(channelId);

            if (player is null)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                return;
            }

            if (player.CurrentTrack is null)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NO_AUDIO_PLAYING)));
                return;
            }

            if (perServerSession[serverId].IsPaused)
            {
                await player.ResumeAsync();
                perServerSession[serverId].IsPaused = false;

                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.RESUMED)));
            }
        }

        private bool AreBotAndCallerInTheSameChannel(CommandContext ctx)
        {
            if (ctx.Member?.VoiceState is null)
            {
                return false;
            }

            var player = GetPlayer(ctx.Guild.Id);
            return player is not null && player.VoiceChannelId == ctx.Member.VoiceState.Channel.Id;
        }

        [Command("queue")]
        public async Task QueueCommand(CommandContext ctx, [RemainingText] string pageStr)
        {
            if (!AreBotAndCallerInTheSameChannel(ctx))
            {
                return;
            }

            int page = 1;
            if (int.TryParse(pageStr, out int parsedPage) && parsedPage > 0)
            {
                page = parsedPage;
            }

            await Queue(ctx.Client, ctx.Guild.Id, ctx.Channel.Id, page);
        }

        private async Task Queue(DiscordClient client, ulong serverId, ulong channelId, int page)
        {
            const ushort amountPerPage = 50;
            var channel = await client.GetChannelAsync(channelId);
            var player = GetPlayer(serverId);

            if (player is null)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                return;
            }

            if (perServerSession[serverId].HeavyOperationOngoing)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.HEAVY_OPERATION_ONGOING)));
                return;
            }

            if (perServerSession[serverId].Queue.Count <= 0)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NO_ELEMENTS_IN_QUEUE)));
                return;
            }

            int pages = (int)Math.Ceiling((double)perServerSession[serverId].Queue.Count / amountPerPage);
            page = Math.Min(page, pages);

            int x = (page - 1) * amountPerPage;
            int count = Math.Min(perServerSession[serverId].Queue.Count - x, amountPerPage);
            string queueInfo = $"📄 {page}/{pages}\n\n";

            int index = x + 1;
            foreach (NewLavalinkTrack t in perServerSession[serverId].Queue.Skip(x).Take(count))
            {
                queueInfo += $"{index}: {t.FinalTitle}\n";
                index++;
            }

            await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(queueInfo));
        }

        [Command("remove")]
        public async Task RemoveCommand(CommandContext ctx, int position)
        {
            if (!AreBotAndCallerInTheSameChannel(ctx))
            {
                return;
            }

            ulong serverId = ctx.Guild.Id;

            if (perServerSession[serverId].HeavyOperationOngoing)
            {
                await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.HEAVY_OPERATION_ONGOING)));
                return;
            }

            var player = GetPlayer(serverId);
            if (player is null)
            {
                await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                return;
            }

            DiscordChannel channel = ctx.Channel;

            if (position <= perServerSession[serverId].Queue.Count)
            {
                position--;
                int index = 0;

                foreach (NewLavalinkTrack t in perServerSession[serverId].Queue.ToList())
                {
                    if (position == index)
                    {
                        perServerSession[serverId].Queue.Remove(t);

                        await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.DELETED, t.FinalTitle)));
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

        [Command("clear")]
        public async Task ClearCommand(CommandContext ctx)
        {
            if (!AreBotAndCallerInTheSameChannel(ctx))
            {
                return;
            }

            ulong serverId = ctx.Guild.Id;

            if (perServerSession[serverId].HeavyOperationOngoing)
            {
                await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.HEAVY_OPERATION_ONGOING)));
                return;
            }

            var player = GetPlayer(serverId);
            if (player is null)
            {
                await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                return;
            }

            perServerSession[serverId].Queue.Clear();

            await ctx.Channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.CLEARED)));
        }

        [Command("fmradio")]
        public async Task FmRadioCommand(CommandContext ctx, [RemainingText] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.EMPTY_FMRADIO_QUERY)));
                return;
            }

            if (ctx.Member?.VoiceState is null)
            {
                await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NOT_IN_A_VOICE_CHANNEL)));
                return;
            }

            var results = await RadioBrowserController.SearchStationsByNameAsync(query);

            if (results.Count == 0)
            {
                await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NO_RADIOS_FOUND_FOR_SEARCH, query)));
                return;
            }

            if(!perServerSession.ContainsKey(ctx.Guild.Id))
            {
                perServerSession[ctx.Guild.Id] = new LavalinkVoiceSession
                {
                    RadioSelectionCommandContext = ctx,
                    CallerTextChannelId = ctx.Channel.Id
                };
            }

            string customId = $"fmradio_select:{Guid.NewGuid():N}";

            fmRadioSelections[customId] = new FmRadioSelectionSession
            {
                UserId = ctx.User.Id,
                Results = results,
            };

            var options = new List<DiscordSelectComponentOption>();
            for (int x = 0; x < results.Count; x++)
            {
                string name = results[x].Name;
                if(name.Length > 50)
                {
                    name = name.Substring(0, 47) + "...";
                }
                string description = RadioBrowserController.BuildRadioDescription(results[x]);
                if(description.Length > 70)
                {
                    description = description.Substring(0, 67) + "...";
                }
                options.Add(new DiscordSelectComponentOption(name,x.ToString(),description));
            }

            var builder = new DiscordMessageBuilder()
                .AddEmbed(new DiscordEmbedBuilder().WithTitle(messageManager.GetMessage(Message.SELECT_FMRADIO)))
                .AddComponents(new DiscordSelectComponent(
                    customId,
                    messageManager.GetMessage(Message.SELECT_FMRADIO),
                    options,
                    false,
                    1,
                    1));

            await ctx.Channel.SendMessageAsync(builder);
        }

        [Command("skip")]
        public async Task SkipCommand(CommandContext ctx)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                await Skip(ctx.Client, ctx.Guild.Id, ctx.Channel.Id);
            }
        }

        private async Task Skip(DiscordClient client, ulong serverId, ulong channelId)
        {
            var player = GetPlayer(serverId);
            var channel = await client.GetChannelAsync(channelId);

            if (player is null)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                return;
            }

            if (player.CurrentTrack is not null)
            {
                perServerSession[serverId].IsPaused = false;
                await player.StopAsync();

                if (!perServerSession[serverId].IsPlayingJoinAudio)
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.SKIPPED)));
                }
            }
        }

        [Command("stop")]
        public async Task StopCommand(CommandContext ctx)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                await Stop(ctx.Client, ctx.Guild.Id, ctx.Channel.Id);
            }
        }

        private async Task Stop(DiscordClient client, ulong serverId, ulong channelId)
        {
            var player = GetPlayer(serverId);
            DiscordChannel channel = await client.GetChannelAsync(channelId);

            if (player is null)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                return;
            }

            if (perServerSession[serverId].HeavyOperationOngoing)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.HEAVY_OPERATION_ONGOING)));
                return;
            }

            await DisconnectFromVoiceChannel(player, channel, serverId);
        }

        [Command("shuffle")]
        public async Task ShuffleCommand(CommandContext ctx)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                await Shuffle(ctx.Client, ctx.Guild.Id, ctx.Channel.Id);
            }
        }

        private async Task Shuffle(DiscordClient client, ulong serverId, ulong channelId)
        {
            var player = GetPlayer(serverId);
            var channel = await client.GetChannelAsync(channelId);

            if (perServerSession[serverId].HeavyOperationOngoing)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.HEAVY_OPERATION_ONGOING)));
                return;
            }

            if (player is null)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                return;
            }

            if (perServerSession[serverId].Queue.Count <= 0)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NO_ELEMENTS_IN_QUEUE)));
                return;
            }

            Random r = new();
            NewLavalinkTrack[] newLavalinkTracks = perServerSession[serverId].Queue.ToArray();

            r.Shuffle(newLavalinkTracks);
            perServerSession[serverId].Queue.Clear();

            foreach (NewLavalinkTrack t in newLavalinkTracks)
            {
                perServerSession[serverId].Queue.AddLast(t);
            }

            await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.SHUFFLED)));
        }

        [Command("move")]
        public async Task MoveCommand(CommandContext ctx, int fromPosition, int toPosition)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                await Move(ctx.Client, ctx.Guild.Id, ctx.Channel.Id, fromPosition, toPosition);
            }
        }

        private async Task Move(DiscordClient client, ulong serverId, ulong channelId, int fromPosition, int toPosition)
        {
            var player = GetPlayer(serverId);
            var channel = await client.GetChannelAsync(channelId);

            if (player is null)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                return;
            }

            if (perServerSession[serverId].HeavyOperationOngoing)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.HEAVY_OPERATION_ONGOING)));
                return;
            }

            if (perServerSession[serverId].Queue.Count <= 0)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NO_ELEMENTS_IN_QUEUE)));
                return;
            }

            if (fromPosition < 1 || fromPosition > perServerSession[serverId].Queue.Count ||toPosition < 1 || toPosition > perServerSession[serverId].Queue.Count)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.OUT_OF_RANGE_IN_QUEUE)));
                return;
            }

            if (fromPosition == toPosition)
            {
                return;
            }

            List<NewLavalinkTrack> queueList = perServerSession[serverId].Queue.ToList();
            NewLavalinkTrack movedTrack = queueList[fromPosition - 1];

            queueList.RemoveAt(fromPosition - 1);
            queueList.Insert(toPosition - 1, movedTrack);

            perServerSession[serverId].Queue.Clear();

            foreach (NewLavalinkTrack track in queueList)
            {
                perServerSession[serverId].Queue.AddLast(track);
            }

            await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.MOVED_IN_QUEUE, movedTrack.FinalTitle, fromPosition, toPosition)));
        }

        [Command("repeat")]
        public async Task RepeatCommand(CommandContext ctx, [RemainingText] string repetitionsStr)
        {
            if (!AreBotAndCallerInTheSameChannel(ctx))
            {
                return;
            }

            ulong serverId = ctx.Guild.Id;

            if (perServerSession[serverId].ArtistRadioMode)
            {
                await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.ARTIST_RADIO_MODE_ENABLED)));
                return;
            }

            if (perServerSession[serverId].RadioMode)
            {
                await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.RADIO_MODE_IS_ENABLED)));
                return;
            }

            int repetitions = 1;
            if (int.TryParse(repetitionsStr, out int parsedRepetitions) && parsedRepetitions > 0)
            {
                repetitions = parsedRepetitions;
            }

            if (botParams.perServerQueueLimit < repetitions + perServerSession[serverId].Queue.Count)
            {
                await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.OUT_OF_RANGE_IN_QUEUE)));
                return;
            }

            if (perServerSession[serverId].HeavyOperationOngoing)
            {
                await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.HEAVY_OPERATION_ONGOING)));
                return;
            }

            var player = GetPlayer(serverId);
            if (player is null)
            {
                await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                return;
            }

            if (player.CurrentTrack is not null)
            {
                for (int i = 0; i < repetitions; i++)
                {
                    perServerSession[serverId].Queue.AddFirst(new NewLavalinkTrack(perServerSession[serverId].LastTrackPlayed!));
                }

                await ctx.Channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.REPEATED, repetitions.ToString().Trim())));
            }
        }

        [Command("reverse")]
        public async Task ReverseCommand(CommandContext ctx)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                await Reverse(ctx.Client, ctx.Guild.Id, ctx.Channel.Id);
            }
        }

        private async Task Reverse(DiscordClient client, ulong serverId, ulong channelId)
        {
            var player = GetPlayer(serverId);
            var channel = await client.GetChannelAsync(channelId);

            if (player is null)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                return;
            }

            if (perServerSession[serverId].HeavyOperationOngoing)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.HEAVY_OPERATION_ONGOING)));
                return;
            }

            if (perServerSession[serverId].Queue.Count <= 0)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NO_ELEMENTS_IN_QUEUE)));
                return;
            }

            List<NewLavalinkTrack> queueList = perServerSession[serverId].Queue.ToList();
            queueList.Reverse();

            perServerSession[serverId].Queue.Clear();

            foreach (NewLavalinkTrack track in queueList)
            {
                perServerSession[serverId].Queue.AddLast(track);
            }

            await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.QUEUE_REVERSED)));
        }

        [Command("loop")]
        public async Task LoopCommand(CommandContext ctx)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                await Loop(ctx.Client, ctx.Guild.Id, ctx.Channel.Id);
            }
        }

        private async Task Loop(DiscordClient client, ulong serverId, ulong channelId)
        {
            var player = GetPlayer(serverId);
            var channel = await client.GetChannelAsync(channelId);

            if (player is null)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                return;
            }

            if (perServerSession[serverId].HeavyOperationOngoing)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.HEAVY_OPERATION_ONGOING)));
                return;
            }

            if (player.CurrentTrack is null)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NO_AUDIO_PLAYING)));
                return;
            }

            if (perServerSession[serverId].ArtistRadioMode)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.ARTIST_RADIO_MODE_ENABLED)));
                return;
            }

            if (perServerSession[serverId].RadioMode)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.RADIO_MODE_IS_ENABLED)));
                return;
            }

            perServerSession[serverId].Loop = !perServerSession[serverId].Loop;

            if (perServerSession[serverId].Loop)
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.LOOP)));
            else
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.LOOP_DISABLED)));
        }


        [Command("volume")]
        public async Task VolumeCommand(CommandContext ctx, int volume)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                await Volume(ctx.Client, ctx.Guild.Id, ctx.Channel.Id, volume);
            }
        }

        private async Task Volume(DiscordClient client, ulong serverId, ulong channelId, int volume)
        {
            var player = GetPlayer(serverId);
            var channel = await client.GetChannelAsync(channelId);

            if (player is null)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                return;
            }

            if (perServerSession[serverId].HeavyOperationOngoing)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.HEAVY_OPERATION_ONGOING)));
                return;
            }

            if (player.CurrentTrack is null)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NO_AUDIO_PLAYING)));
                return;
            }

            if (volume < MinVolume || volume > MaxVolume)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.VOLUME_OUT_OF_RANGE, MinVolume, MaxVolume)));
                return;
            }

            await ApplyVolume(player, serverId, volume);

            await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.VOLUME_SET, volume)));
        }

        private async Task AdjustVolume(DiscordClient client, ulong serverId, ulong channelId, int delta)
        {
            var player = GetPlayer(serverId);
            var channel = await client.GetChannelAsync(channelId);

            if (player is null)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.GET_LAVALINK_CONNECTION_ERROR)));
                return;
            }

            if (perServerSession[serverId].HeavyOperationOngoing)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.HEAVY_OPERATION_ONGOING)));
                return;
            }

            if (player.CurrentTrack is null)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NO_AUDIO_PLAYING)));
                return;
            }

            int currentVolume = perServerSession[serverId].Volume;
            int newVolume = Math.Clamp(currentVolume + delta, MinVolume, MaxVolume);

            if (newVolume == currentVolume)
            {
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.VOLUME_ALREADY_AT, currentVolume)));
                return;
            }

            await ApplyVolume(player, serverId, newVolume);

            await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.VOLUME_SET, newVolume)));
        }

        private async Task ApplyVolume(QueuedLavalinkPlayer player, ulong serverId, int volume)
        {
            volume = Math.Clamp(volume, MinVolume, MaxVolume);
            float newVolume = volume / 100f;
            perServerSession[serverId].Volume = volume;

            await player.SetVolumeAsync(newVolume);
        }



        private void CleanupSession(ulong serverId)
        {
            if (perServerSession.ContainsKey(serverId))
            {
                perServerSession[serverId].Queue.Clear();
                perServerSession[serverId].RadioMode = false;
                perServerSession[serverId].ArtistRadioMode = false;
                perServerSession.Remove(serverId);
            }

            activePlayers.Remove(serverId);
            playbackWatchers.Remove(serverId);
        }

        private async Task DisconnectFromVoiceChannel(QueuedLavalinkPlayer player, DiscordChannel channel, ulong serverId)
        {
            await player.DisconnectAsync();
            CleanupSession(serverId);

            await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.STOPPED)));
        }

        private async Task HandlePauseButton(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            ulong serverId = e.Guild.Id;

            if (perServerSession.ContainsKey(serverId))
            {
                if (perServerSession[serverId].IsPaused)
                {
                    await Resume(client, serverId, e.Channel.Id);
                }
                else
                {
                    await Pause(client, serverId, e.Channel.Id);
                }
            }

            await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
        }

        private async Task HandleSkipButton(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            ulong serverId = e.Guild.Id;

            if (perServerSession.ContainsKey(serverId))
            {
                await Skip(client, serverId, e.Channel.Id);
            }

            await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
        }

        private async Task HandleStopButton(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            ulong serverId = e.Guild.Id;

            if (perServerSession.ContainsKey(serverId))
            {
                await Stop(client, serverId, e.Channel.Id);
            }

            await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
        }

        private async Task HandleLoopButton(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            ulong serverId = e.Guild.Id;

            if (perServerSession.ContainsKey(serverId))
            {
                await Loop(client, serverId, e.Channel.Id);
            }

            await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
        }

        private async Task HandleQueueButton(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            ulong serverId = e.Guild.Id;

            if (perServerSession.ContainsKey(serverId))
            {
                await Queue(client, serverId, e.Channel.Id, 1);
            }

            await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
        }

        private async Task HandleShuffleButton(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            ulong serverId = e.Guild.Id;

            if (perServerSession.ContainsKey(serverId))
            {
                await Shuffle(client, serverId, e.Channel.Id);
            }

            await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
        }

        private async Task HandleVolumeDownButton(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            ulong serverId = e.Guild.Id;

            if (perServerSession.ContainsKey(serverId))
            {
                await AdjustVolume(client, serverId, e.Channel.Id, -VolumeStep);
            }

            await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
        }

        private async Task HandleVolumeUpButton(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            ulong serverId = e.Guild.Id;

            if (perServerSession.ContainsKey(serverId))
            {
                await AdjustVolume(client, serverId, e.Channel.Id, VolumeStep);
            }

            await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
        }

        private async Task HandleFmRadioSelect(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            if (!fmRadioSelections.TryGetValue(e.Id, out var session))
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .AsEphemeral(true)
                        .AddEmbed(CustomEmbedBuilder.CreateEmbed(
                            messageManager.GetMessage(Message.FMRADIO_SELECTION_EXPIRED))));
                return;
            }

            if (session.UserId != e.User.Id)
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .AsEphemeral(true)
                        .AddEmbed(CustomEmbedBuilder.CreateEmbed(
                            messageManager.GetMessage(Message.FMRADIO_SELECTION_NOT_FOR_YOU))));
                return;
            }

            if (!int.TryParse(e.Values.FirstOrDefault(), out int index) || index < 0 || index >= session.Results.Count)
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .AsEphemeral(true)
                        .AddEmbed(CustomEmbedBuilder.CreateEmbed(
                            messageManager.GetMessage(Message.FMRADIO_SELECTION_EXPIRED))));
                return;
            }

            var station = session.Results[index];
            fmRadioSelections.Remove(e.Id);

            await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);

            string streamUrl = RadioBrowserController.GetPlayableUrl(station);

            if(!string.IsNullOrEmpty(streamUrl))
            {
                Tuple<List<NewLavalinkTrack>, bool> tuple;
                perServerSession[e.Guild.Id].HeavyOperationOngoing = true;
                try
                {
                    tuple = await LavalinkController.loadLavalinkTrack(Program.AudioService, streamUrl, false, perServerSession[e.Guild.Id].Queue.Count);
                }
                finally
                {
                    perServerSession[e.Guild.Id].HeavyOperationOngoing = false;
                }

                if (tuple.Item2)
                {
                    await e.Channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.QUEUE_LIMIT_REACHED)));
                    return;
                }
                else
                {
                    if (tuple.Item1.Count == 0)
                    {
                        await e.Channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.NO_TRACKS_FOUND_FOR_SEARCH, streamUrl)));
                    }
                    else
                    {
                        CommandContext recovered = perServerSession[e.Guild.Id].RadioSelectionCommandContext!;
                        if (recovered is not null)
                        {
                            await PlayInternal(recovered, streamUrl, false, true);
                        }
                        else
                        {
                            Console.WriteLine("Error: recovered CommandContext was null after fmradio selection, cannot play stream");
                        }
                    }
                }
            }
            else
            {
                var channel = await client.GetChannelAsync(e.Channel.Id);
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.RADIO_STREAM_UNAVAILABLE)));
            }
        }




    }
}