using DSharpPlus.CommandsNext;
using Gargabot.Parameters;
using Gargabot.Messages;
using DSharpPlus.CommandsNext.Attributes;
using Gargabot.Utils.DiscordUtils;

namespace Gargabot.Commands
{
    public class UniversalCommandModule : BaseCommandModule
    {   
        protected BotParameters botParams = BotParameters.LoadFromJson(BotParameters.GetAppSettingsPath());
        protected MessageManager messageManager = new MessageManager(BotParameters.GetMessagesPath());

        [Command("help")]
        public virtual async Task Help(CommandContext ctx)
        {
            await ctx.RespondAsync(CustomEmbedBuilder.CreateEmbed(messageManager.GetMessage("HELP", botParams.prefix)));
        }

    }

}
