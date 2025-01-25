using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gargabot.AudioSessions
{
    public class VoiceSession
    {
        private ulong callerTextChannelId;
        private bool isPlayingJoinAudio, isPaused, isSkipped, joined, isPlaying, heavyOperationOngoing, loop;

        public VoiceSession()
        {
            isPaused = false;
            joined = false;
            isSkipped = false;
            isPaused = false;
            isPlayingJoinAudio = false;
            callerTextChannelId = 0;
            heavyOperationOngoing = false;
            loop = false;
        }
        public bool IsPaused { get => isPaused; set => isPaused = value; }
        public bool IsPlaying { get => isPlaying; set => isPlaying = value; }
        public bool Joined { get => joined; set => joined = value; }
        public bool IsPlayingJoinAudio { get => isPlayingJoinAudio; set => isPlayingJoinAudio = value; }
        public ulong CallerTextChannelId { get => callerTextChannelId; set => callerTextChannelId = value; }
        public bool IsSkipped { get => isSkipped; set => isSkipped = value; }
        public bool HeavyOperationOngoing { get => heavyOperationOngoing; set => heavyOperationOngoing = value; }
        public bool Loop { get => loop; set => loop = value; }
    }
}
