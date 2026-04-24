using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.EventArgs;
using Gargabot.Commands;
using Gargabot.Exceptions;
using Gargabot.Parameters;
using Lavalink4NET;
using Lavalink4NET.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Gargabot
{
    internal class Program
    {
        public delegate Task ButtonActionDelegate(DiscordClient client, ComponentInteractionCreateEventArgs e);

        public static event ButtonActionDelegate? OnPauseButtonPressed;
        public static event ButtonActionDelegate? OnSkipButtonPressed;
        public static event ButtonActionDelegate? OnLoopButtonPressed;
        public static event ButtonActionDelegate? OnStopButtonPressed;
        public static event ButtonActionDelegate? OnQueueButtonPressed;
        public static event ButtonActionDelegate? OnShuffleButtonPressed;
        public static event ButtonActionDelegate? OnVolumeDownButtonPressed;
        public static event ButtonActionDelegate? OnVolumeUpButtonPressed;
        public static event ButtonActionDelegate? OnFmRadioSelectPressed;

        public static DiscordClient Discord { get; private set; } = null!;
        public static IAudioService AudioService { get; private set; } = null!;
        public static IServiceProvider Services { get; private set; } = null!;

        static async Task Main(string[] args)
        {
            BotParameters botParams = BotParameters.LoadFromJson(BotParameters.GetAppSettingsPath());

            if (!botParams.IsComplete())
            {
                return;
            }

            Discord = new DiscordClient(new DiscordConfiguration()
            {
                Token = botParams.discord_token,
                TokenType = TokenType.Bot,
                Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents,
            });

            //Lavalink is the new default
            try
            {
                var services = new ServiceCollection();

                services.AddSingleton(Discord);
                services.AddLogging(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Information);
                });

                services.AddLavalink();
                services.ConfigureLavalink(config =>
                {
                    config.BaseAddress = new Uri($"http://{botParams.lavalinkCredentials!.host}:{botParams.lavalinkCredentials.port}");
                    config.Passphrase = botParams.lavalinkCredentials.password!;
                    config.ReadyTimeout = TimeSpan.FromSeconds(20);
                });

                Services = services.BuildServiceProvider();

                var commands = Discord.UseCommandsNext(new CommandsNextConfiguration
                {
                    EnableMentionPrefix = true,
                    CaseSensitive = false,
                    EnableDms = false,
                    StringPrefixes = new[] { botParams.prefix }!,
                    IgnoreExtraArguments = true,
                    EnableDefaultHelp = false,
                    Services = Services,
                });

                Discord.ComponentInteractionCreated += async (client, e) =>
                {
                    if (e.Id.StartsWith("fmradio_select:"))
                    {
                        if (OnFmRadioSelectPressed is not null)
                            await OnFmRadioSelectPressed.Invoke(client, e);

                        return;
                    }
                    switch (e.Id)
                    {
                        case "pause_button":
                            if (OnPauseButtonPressed is not null)
                                await OnPauseButtonPressed.Invoke(client, e);
                            break;

                        case "skip_button":
                            if (OnSkipButtonPressed is not null)
                                await OnSkipButtonPressed.Invoke(client, e);
                            break;

                        case "loop_button":
                            if (OnLoopButtonPressed is not null)
                                await OnLoopButtonPressed.Invoke(client, e);
                            break;

                        case "stop_button":
                            if (OnStopButtonPressed is not null)
                                await OnStopButtonPressed.Invoke(client, e);
                            break;

                        case "queue_button":
                            if (OnQueueButtonPressed is not null)
                                await OnQueueButtonPressed.Invoke(client, e);
                            break;

                        case "shuffle_button":
                            if (OnShuffleButtonPressed is not null)
                                await OnShuffleButtonPressed.Invoke(client, e);
                            break;
                        case "volume_down_button":
                            if (OnVolumeDownButtonPressed is not null)
                                await OnVolumeDownButtonPressed.Invoke(client, e);
                            break;

                        case "volume_up_button":
                            if (OnVolumeUpButtonPressed is not null)
                                await OnVolumeUpButtonPressed.Invoke(client, e);
                            break;
                    }
                };

                commands.RegisterCommands<UniversalCommandModule>();
                commands.RegisterCommands<LavalinkCommandModule>();

                await Discord.ConnectAsync();

                AudioService = Services.GetRequiredService<IAudioService>();
                await AudioService.StartAsync();

                await Task.Delay(-1);
            }
            catch (Exception ex)
            {
                throw new InvalidLavalinkSession(ex.ToString());
            }
                   
        }
    }
}