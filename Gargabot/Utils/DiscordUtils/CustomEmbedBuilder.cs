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

        public static DiscordMessageBuilder CreateEmbedWithButtons(DiscordEmbedBuilder embed, string[] ids, string[] buttonLabels)
        {
            DiscordMessageBuilder builder = new DiscordMessageBuilder();
            builder.AddEmbed(embed);

            for(int x=0; x<ids.Length; x++)
            {
                builder.AddComponents(new DiscordButtonComponent(ButtonStyle.Primary, ids[x], buttonLabels[x]));
            }

            return builder;
        }

    }
}
