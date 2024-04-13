using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext;
using DSharpPlus.VoiceNext;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Gargabot.Parameters;
using Gargabot.Messages;
using DSharpPlus.Entities;
using Gargabot.Utils.DiscordUtils;
using Gargabot.Utils;
using Gargabot.AudioSessions;

namespace Gargabot.Commands
{
    [Obsolete("This class is obsolete, use Lavalink instead")]
    public class VoiceNextCommandModule : UniversalCommandModule
    {
        private Dictionary<ulong, VoiceNextVoiceSession> perServerSession = new Dictionary<ulong, VoiceNextVoiceSession>();

        [Command("play")]
        public async Task PlayCommand(CommandContext ctx, [RemainingText] string source)
        {
            var vnext = ctx.Client.GetVoiceNext();
            var connection = vnext.GetConnection(ctx.Guild);
            ulong channelId = ctx.Channel.Id;
            DiscordChannel channel = ctx.Guild.GetChannel(channelId);

            ulong serverId = ctx.Guild.Id;
            if(!perServerSession.ContainsKey(serverId))
            {
                VoiceNextVoiceSession vs = new VoiceNextVoiceSession();
                perServerSession.Add(serverId, vs);
            }

            perServerSession[serverId].Joined = false;

            if (!AreBotAndCallerInTheSameChannel(ctx))
            {
                if (ctx.Member.VoiceState == null)
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("NOT_IN_A_VOICE_CHANNEL")));
                    return;
                }
                else
                {
                    if (connection == null)
                    {
                        await ctx.Member.VoiceState.Channel.ConnectAsync();
                        perServerSession[serverId].Joined = true;
                    }
                    else
                    {
                        await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("BOT_ALREADY_IN_ANOTHER_VOICE_CHANNEL")));
                        return;
                    }
                }
            }

            if (perServerSession[serverId].Joined || AreBotAndCallerInTheSameChannel(ctx))
            {
                await ctx.TriggerTypingAsync();
                source.Trim();
                string ytPattern = @"^(https?://)?(www\.)?(youtube\.com|youtu\.be)/watch\?v=[A-Za-z0-9_-]{11}$";
                Regex regex = new Regex(ytPattern);
                Audio a;
                if (regex.IsMatch(source))
                {
                    //Es una URL de Youtube válida
                    a = new Audio(source, "");
                    perServerSession[serverId].Queue.AddLast(a);
                    //Falta analizar los casos de en vivos
                }
                else
                {
                    string ytUrl= YtDlpUtils.GetYoutubeRealURL(source).Trim();
                    if (ytUrl != "")
                    {
                        a = new Audio(ytUrl, "");
                        perServerSession[serverId].Queue.AddLast(a);
                        source = ytUrl;
                    }
                    else
                    {
                        await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("NO_TRACKS_FOUND_FOR_SEARCH", source)));
                        return;
                    }
                }

                
                if (!perServerSession[serverId].IsPlaying)
                {
                    await PlayNextTrack(ctx);
                }
                else
                {
                    a.Title = YtDlpUtils.GetYoutubeVideoTitle(source);
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(a.Url, a.Title, messageManager.GetMessage("ADDED_TO_QUEUE_IN_POSITION", perServerSession[serverId].Queue.Count)));
                }
            }
        }

        [Command("stop")]
        public async Task StopCommand(CommandContext ctx)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                ulong serverId = ctx.Guild.Id;
                perServerSession[serverId].IsPlaying = false;
                var vnext = ctx.Client.GetVoiceNext();
                var connection = vnext.GetConnection(ctx.Guild);
                ulong channelId = ctx.Channel.Id;
                DiscordChannel channel = ctx.Guild.GetChannel(channelId);
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("STOPPED")));
                connection.Disconnect();

                perServerSession.Remove(serverId);



            }
        }

        [Command("pause")]
        public async Task PauseCommand(CommandContext ctx)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                ulong serverId = ctx.Guild.Id;
                var vnext = ctx.Client.GetVoiceNext();
                var connection = vnext.GetConnection(ctx.Guild);
                ulong channelId = ctx.Channel.Id;
                DiscordChannel channel = ctx.Guild.GetChannel(channelId);
                perServerSession[serverId].IsPaused = true;
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("PAUSED")));
                connection.Pause();

            }
        }

        [Command("resume")]
        public async Task ResumeCommand(CommandContext ctx)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                ulong serverId = ctx.Guild.Id;
                var vnext = ctx.Client.GetVoiceNext();
                var connection = vnext.GetConnection(ctx.Guild);
                ulong channelId = ctx.Channel.Id;
                DiscordChannel channel = ctx.Guild.GetChannel(channelId);
                perServerSession[serverId].IsPaused = false;
                await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("RESUMED")));
                await connection.ResumeAsync();
            }
        }

        [Command("queue")]
        public async Task QueueCommand(CommandContext ctx)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                ulong serverId = ctx.Guild.Id;
                ulong channelId = ctx.Channel.Id;
                DiscordChannel channel = ctx.Guild.GetChannel(channelId);
                if (perServerSession[serverId].Queue.Count > 0)
                {
                    int index = 1;
                    string queueInfo = "";
                    foreach (Audio a in perServerSession[serverId].Queue)
                    {
                        if (a.Title.Trim() == "")
                            UpdateAudioTitle(a);
                        queueInfo += $"{index}: {a.Title}\n";
                        index++;
                    }
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(queueInfo));
                }
                else
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("NO_ELEMENTS_IN_QUEUE")));
                }
            }
        }

        [Command("skip")]
        public async Task SkipCommand(CommandContext ctx)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                ulong serverId = ctx.Guild.Id;
                ulong channelId = ctx.Channel.Id;
                DiscordChannel channel = ctx.Guild.GetChannel(channelId);
                if (perServerSession[serverId].IsPlaying)
                {
                    var vnext = ctx.Client.GetVoiceNext();
                    var connection = vnext.GetConnection(ctx.Guild);
                    perServerSession[serverId].Cts.Cancel();
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("SKIPPED")));
                }
                else
                {
                    await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("NO_AUDIO_PLAYING")));
                }
            }
        }

        [Command("remove")]
        public async Task RemoveCommand(CommandContext ctx, int position)
        {
            if (AreBotAndCallerInTheSameChannel(ctx))
            {
                ulong serverId = ctx.Guild.Id;
                ulong channelId = ctx.Channel.Id;
                DiscordChannel channel = ctx.Guild.GetChannel(channelId);
                position--;
                if (position <= perServerSession[serverId].Queue.Count)
                {
                    int index = 0;
                    foreach (Audio a in perServerSession[serverId].Queue)
                    {
                        if (position==index)
                        {
                            perServerSession[serverId].Queue.Remove(a);
                            await channel.SendMessageAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("BOT_ALREADY_IN_ANOTHER_VOICE_CHANNEL")));
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

        private async Task PlayNextTrack(CommandContext ctx)
        {
            ulong serverId = ctx.Guild.Id;
            perServerSession[serverId].IsPlaying = true;
            var vnext = ctx.Client.GetVoiceNext();
            var connection = vnext.GetConnection(ctx.Guild);
            ulong channelId = ctx.Channel.Id;
            DiscordChannel channel = ctx.Guild.GetChannel(channelId);

            if (connection == null)
            {
                perServerSession[serverId].IsPlaying = false;
                return;
            }

            if (perServerSession[serverId].IsPaused)
            {
                await connection.ResumeAsync();
            }

            var transmit = connection.GetTransmitSink();
            perServerSession[serverId].Cts = new CancellationTokenSource();

            Audio a = perServerSession[serverId].Queue.First.Value;
            perServerSession[serverId].Queue.RemoveFirst();

            if (a.Title == "")
            {
                UpdateAudioTitle(a);
            }

            await channel.SendMessageAsync((CustomEmbedBuilder.CreateEmbed(a.Url, a.Title, messageManager.GetMessage("PLAYING_ON", ctx.Guild.Name))));

            var pcm = FFmpegUtils.ConvertYoutube(a.Url, perServerSession[serverId].Cts.Token);

            await pcm.CopyToAsync(transmit);
            await pcm.DisposeAsync();

            if (perServerSession[serverId].Queue.Count > 0)
            {
                await PlayNextTrack(ctx);
            }
            else
            {
                perServerSession[serverId].IsPlaying = false;
            }
        }

        private bool AreBotAndCallerInTheSameChannel(CommandContext ctx)
        {
            var vnext = ctx.Client.GetVoiceNext();
            var connection = vnext.GetConnection(ctx.Guild);
            return connection!=null && connection.TargetChannel == ctx.Member.VoiceState.Channel;
        }

        private void UpdateAudioTitle(Audio a)
        {
            if (a.Url != "")
            {
                a.Title = YtDlpUtils.GetYoutubeVideoTitle(a.Url);
            }
        }



    }
}
