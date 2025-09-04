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
        private bool radioMode;
        private NewLavalinkTrack lastTrackPlayed;
        private Dictionary<string, bool> currentRadioHistory;
        private bool artistRadioMode;
        private string artistRadioArtistId;

        public LavalinkVoiceSession()
        {
            queue = new LinkedList<NewLavalinkTrack>();
            IsPaused = false;
            Joined = false;
            IsSkipped = false;
            IsPlayingJoinAudio = false;
            RadioMode = false;
            artistRadioMode = false;
            CallerTextChannelId = 0;

        }

        public LinkedList<NewLavalinkTrack> Queue { get => queue; }
        public bool RadioMode { get => radioMode; set => radioMode = value; }
        public NewLavalinkTrack LastTrackPlayed { get => lastTrackPlayed; set => lastTrackPlayed = value; }
        public Dictionary<string, bool> CurrentRadioHistory { get => currentRadioHistory; set => currentRadioHistory = value; }
        public bool ArtistRadioMode { get => artistRadioMode; set => artistRadioMode = value; }
        public string ArtistRadioArtistId { get => artistRadioArtistId; set => artistRadioArtistId = value; }

    }
}
