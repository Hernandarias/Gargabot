using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gargabot.AudioSessions
{
    public class VoiceNextVoiceSession : VoiceSession
    {
        private readonly LinkedList<Audio> queue;
        private CancellationTokenSource cts;

        public VoiceNextVoiceSession()
        {
            queue = new LinkedList<Audio>();
            IsPaused = false;
            cts = new CancellationTokenSource();
            Joined = false;
            IsSkipped = false;
            IsPlayingJoinAudio = false;
            CallerTextChannelId = 0;
        }

        public LinkedList<Audio> Queue => queue;
        public CancellationTokenSource Cts { get => cts; set => cts = value; }
    }
}
