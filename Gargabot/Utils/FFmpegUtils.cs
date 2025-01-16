using Gargabot.Parameters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gargabot.Utils
{
    public static class FFmpegUtils
    {
        static BotParameters botParams = BotParameters.LoadFromJson(BotParameters.GetAppSettingsPath());
        public static Stream Convert(string path, CancellationToken ct)
        {
            var ffmpeg = Process.Start(new ProcessStartInfo
            {
                FileName = botParams.ffmpeg_path,
                Arguments = $@"-i ""{path}"" -ac 2 -f s16le -ar 48000 pipe:1",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            ct.Register(() =>
            {
                if (ffmpeg != null && !ffmpeg.HasExited)
                {
                    ffmpeg.Kill();
                }
            });

            return ffmpeg.StandardOutput.BaseStream;
        }

        public static Stream ConvertYoutube(string url, CancellationToken ct)
        {
            string youtubeAudioUrl = YtDlpUtils.GetYoutubeAudioURL(url);
            var ffmpeg = Process.Start(new ProcessStartInfo
            {
                FileName = botParams.ffmpeg_path,
                Arguments = $@"-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 -i ""{youtubeAudioUrl}"" -ac 2 -f s16le -ar 48000 pipe:1",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            ct.Register(() =>
            {
                if (ffmpeg != null && !ffmpeg.HasExited)
                {
                    ffmpeg.Kill();
                }
            });

            return ffmpeg.StandardOutput.BaseStream;
        }

        public static Stream ConvertStream(string streamUrl, CancellationToken ct)
        {
            var ffmpeg = Process.Start(new ProcessStartInfo
            {
                FileName = botParams.ffmpeg_path,
                Arguments = $@"-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 -i ""{streamUrl}"" -ac 2 -f s16le -ar 48000 pipe:1",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            ct.Register(() =>
            {
                if (ffmpeg != null && !ffmpeg.HasExited)
                {
                    ffmpeg.Kill();
                }
            });

            return ffmpeg.StandardOutput.BaseStream;
        }
    }
}
