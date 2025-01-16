using DSharpPlus.CommandsNext;
using Gargabot.Parameters;
using Gargabot.Messages;
using DSharpPlus.CommandsNext.Attributes;
using Gargabot.Utils.DiscordUtils;
using Gargabot.Utils.Spotify;

namespace Gargabot.Commands
{
    public class UniversalCommandModule : BaseCommandModule
    {   
        protected BotParameters botParams = BotParameters.LoadFromJson(BotParameters.GetAppSettingsPath());
        protected MessageController messageManager = new MessageController(BotParameters.GetMessagesPath());

        protected bool isSpotifyEnabled()
        {
            return !(botParams.spotifyCredentials == null || string.IsNullOrEmpty(botParams.spotifyCredentials.clientId) || string.IsNullOrEmpty(botParams.spotifyCredentials.clientSecret));
        }

        [Command("help")]
        public virtual async Task Help(CommandContext ctx)
        {
            await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage(Message.HELP, botParams.prefix)));
        }
    }

}
