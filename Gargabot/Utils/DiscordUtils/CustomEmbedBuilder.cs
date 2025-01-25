using DSharpPlus;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gargabot.Utils.DiscordUtils
{
    public static class CustomEmbedBuilder
    {
        public static DiscordEmbedBuilder CreateEmbed(string url, string title, string description)
        {   
            return new DiscordEmbedBuilder()
            {
                Title = title,
                Description = description,
                Url = url,
                Color = DiscordColor.White
            };

        }

        public static DiscordEmbedBuilder CreateEmbed(string url, string title, string description, string footerText, string image)
        {
            return new DiscordEmbedBuilder
            {
                Title = title,
                Description = description,
                Url = url,
                Color = DiscordColor.White,
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = footerText
                },
                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                {
                    Url = image
                }
            };
        }

        public static DiscordEmbedBuilder CreateEmbed(string url, string title, string description, string footerText)
        {
            return new DiscordEmbedBuilder
            {
                Title = title,
                Description = description,
                Url = url,
                Color = DiscordColor.White,
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = footerText
                }
            };
        }

        public static DiscordEmbedBuilder CreateEmbed(string description)
        {
            return new DiscordEmbedBuilder
            {
                Description = description,
                Color = DiscordColor.White
            };
        }

        public static DiscordMessageBuilder CreatePlayEmbed(DiscordEmbedBuilder embed)
        {
            DiscordMessageBuilder builder = new DiscordMessageBuilder();
            builder.AddEmbed(embed);

            List<DiscordButtonComponent[]> discordButtons = GetButtons();

            foreach (DiscordButtonComponent[] buttons in discordButtons)
                builder.AddComponents(buttons);

            return builder;
        }

        public static List<DiscordButtonComponent[]> GetButtons()
        {
            List<DiscordButtonComponent[]> buttons = new List<DiscordButtonComponent[]>();
            buttons.Add(new DiscordButtonComponent[]
            {
                new DiscordButtonComponent(DSharpPlus.ButtonStyle.Primary, "pause_button", "⏸️",false),
                new DiscordButtonComponent(DSharpPlus.ButtonStyle.Primary, "skip_button", "⏭️", false),
                new DiscordButtonComponent(DSharpPlus.ButtonStyle.Primary, "loop_button", "🔁", false),
                new DiscordButtonComponent(DSharpPlus.ButtonStyle.Primary, "shuffle_button", "🔀", false),
                new DiscordButtonComponent(DSharpPlus.ButtonStyle.Primary, "queue_button", "📜", false),
            });
            buttons.Add(new DiscordButtonComponent[]
            {
                new DiscordButtonComponent(DSharpPlus.ButtonStyle.Danger, "stop_button", "🛑", false)
            });
            return buttons;
        }

        

    }
}
