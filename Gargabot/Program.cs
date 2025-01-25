using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Net;
using DSharpPlus.VoiceNext;
using Gargabot.Commands;
using Gargabot.Parameters;
using DSharpPlus.Lavalink;
using Gargabot.Exceptions;
using Gargabot.Messages;
using Newtonsoft.Json;
using DSharpPlus.EventArgs;

namespace Gargabot
{
    internal class Program
    {

        public delegate Task ButtonActionDelegate(DiscordClient client, ComponentInteractionCreateEventArgs e);

        public static event ButtonActionDelegate OnPauseButtonPressed;
        public static event ButtonActionDelegate OnSkipButtonPressed;
        public static event ButtonActionDelegate OnLoopButtonPressed;
        public static event ButtonActionDelegate OnStopButtonPressed;
        public static event ButtonActionDelegate OnQueueButtonPressed;
        public static event ButtonActionDelegate OnShuffleButtonPressed;

        static async Task Main(string[] args)
        {
            BotParameters botParams = BotParameters.LoadFromJson(BotParameters.GetAppSettingsPath());

            if (botParams.IsComplete())
            {
                var discord = new DiscordClient(new DiscordConfiguration()
                {
                    Token = botParams.discord_token,
                    TokenType = TokenType.Bot,
                    Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents,
                });

                CommandsNextConfiguration cnc = new CommandsNextConfiguration()
                {
                    EnableMentionPrefix = true,
                    CaseSensitive = false,
                    EnableDms = false,
                    StringPrefixes = new string[] { botParams.prefix },
                    IgnoreExtraArguments = true,
                    EnableDefaultHelp = false
                };

                var commands = discord.UseCommandsNext(cnc);

                switch (botParams.lavalinkOrVoiceNext)
                {
                    case "voicenext":
                        try
                        {
                            discord.UseVoiceNext();
                            commands.RegisterCommands<VoiceNextCommandModule>();
                            await discord.ConnectAsync();
                            await Task.Delay(-1);
                        }
                        catch(Exception ex)
                        {
                            throw new InvalidVoiceNextSession(ex.ToString());
                        }
                        break;
                    case "lavalink":
                        try
                        {
                            var endpoint = new ConnectionEndpoint
                            {
                                Hostname = botParams.lavalinkCredentials.host,
                                Port = botParams.lavalinkCredentials.port
                            };

                            var lavalinkConfig = new LavalinkConfiguration
                            {
                                Password = botParams.lavalinkCredentials.password,
                                RestEndpoint = endpoint,
                                SocketEndpoint = endpoint
                            };
                            var lavalink = discord.UseLavalink();

                            discord.ComponentInteractionCreated += async (client, e) =>
                            {
                                var serverId = e.Guild.Id;

                                switch (e.Id)
                                {
                                    case "pause_button":
                                        if (OnPauseButtonPressed != null)
                                            await OnPauseButtonPressed.Invoke(client, e);
                                        break;
                                    case "skip_button":
                                        if (OnSkipButtonPressed != null)
                                            await OnSkipButtonPressed.Invoke(client, e);
                                        break;
                                    case "loop_button":
                                        if (OnLoopButtonPressed != null)
                                            await OnLoopButtonPressed.Invoke(client, e);
                                        break;
                                    case "stop_button":
                                        if (OnStopButtonPressed != null)
                                            await OnStopButtonPressed.Invoke(client, e);
                                        break;
                                    case "queue_button":
                                        if (OnQueueButtonPressed != null)
                                            await OnQueueButtonPressed.Invoke(client, e);
                                        break;
                                    case "shuffle_button":
                                        if (OnShuffleButtonPressed != null)
                                            await OnShuffleButtonPressed.Invoke(client, e);
                                        break;
                                }
                            };


                            commands.RegisterCommands<UniversalCommandModule>();
                            commands.RegisterCommands<LavalinkCommandModule>();                
                            await discord.ConnectAsync();
                            await lavalink.ConnectAsync(lavalinkConfig);
                            await Task.Delay(-1);
                        }
                        catch(Exception ex)
                        {
                            throw new InvalidLavalinkSession(ex.ToString());
                        }
                        break;
                }

                
            }
            
        }


    }
}
