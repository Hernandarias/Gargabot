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

namespace Gargabot
{
    internal class Program
    {
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
