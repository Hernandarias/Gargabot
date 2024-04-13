using DSharpPlus.Lavalink;
using Gargabot.Utils.LavalinkUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gargabot.AudioSessions
{
    public class LavalinkVoiceSession : VoiceSession
    {
        private LinkedList<NewLavalinkTrack> queue;
        public LavalinkVoiceSession()
        {
            queue = new LinkedList<NewLavalinkTrack>();
            IsPaused = false;
            Joined = false;
            IsSkipped = false;
            IsPlayingJoinAudio = false;
            CallerTextChannelId = 0;
        }

        public LinkedList<NewLavalinkTrack> Queue { get => queue; }

    }
}
