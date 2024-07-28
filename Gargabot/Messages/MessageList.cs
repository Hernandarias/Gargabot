using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gargabot.Messages
{
    public static class MessageList
    {
        public static List<string> messages = [
            "NOT_IN_A_VOICE_CHANNEL",
            "GET_LAVALINK_CONNECTION_ERROR",
            "BOT_ALREADY_IN_ANOTHER_VOICE_CHANNEL",
            "NO_TRACKS_FOUND_FOR_SEARCH", //args: {0} - search query
            "ADDED_TO_QUEUE_IN_POSITION", //args: {0} - position
            "MULTIPLE_TRACKS_ADDED_TO_QUEUE", //args: {0} - number of tracks
            "PLAYING_ON", //args: {0} - server name
            "NO_AUDIO_PLAYING",
            "PAUSED",
            "RESUMED",
            "SKIPPED",
            "STOPPED",
            "DELETED", //args: {0} - track title
            "NO_ELEMENTS_IN_QUEUE",
            "OUT_OF_RANGE_IN_QUEUE",
            "SHUFFLED",
            "CLEARED",
            "HELP",
            "HEAVY_OPERATION_ONGOING",
            "QUEUE_LIMIT_REACHED",
            "QUEUE_MUST_BE_EMPTY_FOR_RADIO_MODE",
            "RADIO_MODE_IS_ENABLED",
            "RADIO_MODE_DISABLED",
            "RADIO_MODE_ENABLED",
            "NO_SPOTIFY_CREDENTIALS",
            "ARTIST_RADIO_MODE_ENABLED",
            "ARTIST_NOT_FOUND"
        ];
    }
}
