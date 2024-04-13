using Gargabot.Parameters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Gargabot.Utils
{
    [Obsolete("This class is obsolete, use YoutubeUtils instead")]
    public static class YtDlpUtils
    {
        static BotParameters botParams = BotParameters.LoadFromJson(BotParameters.GetAppSettingsPath());
        public static string GetYoutubeAudioURL(string source)
        {
            var yt = Process.Start(new ProcessStartInfo
            {
                FileName = botParams.yt_dlp_path,
                Arguments = $@"-g {source.Trim()}",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            yt.WaitForExit();

            string rawOutput = yt.StandardOutput.ReadToEnd().Trim();

            string[] urls = Regex.Split(rawOutput, "https://");

            if (urls.Length > 1)
                return "https://" + urls[2];
            else
                return "";
        }

        public static string GetYoutubeVideoTitle(string source)
        {
            var yt = Process.Start(new ProcessStartInfo
            {
                FileName = botParams.yt_dlp_path,
                Arguments = $@"-e {source.Trim()}",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            yt.WaitForExit();

            return yt.StandardOutput.ReadToEnd().Trim();
        }

        public static string GetYoutubeRealURL(string source)
        {
            var yt = Process.Start(new ProcessStartInfo
            {
                FileName = botParams.yt_dlp_path,
                Arguments = $@"--get-id ytsearch:""{source.Trim()}""",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            yt.WaitForExit();

            string rawOutput = yt.StandardOutput.ReadToEnd().Trim();

            if (rawOutput == "")
            {
                return "";
            }
            else
            {
                return "https://youtube.com/watch?v=" + rawOutput;
            }
        }


    }
}
