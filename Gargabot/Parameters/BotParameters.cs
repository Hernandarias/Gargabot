using Gargabot.Exceptions;
using Gargabot.Utils.LavalinkUtilities;
using Gargabot.Utils.Spotify;
using Newtonsoft.Json;

namespace Gargabot.Parameters
{
    public class BotParameters
    {
        public string ?discord_token { get;  set; }
        public string ?prefix { get;  set; }
        public int perServerQueueLimit { get;  set; }

        public LavalinkCredentials ?lavalinkCredentials { get; set; }
        public bool useSpotify { get; set; }
        public SpotifyCredentials ?spotifyCredentials { get;  set; }
        public bool allowJoinAudio { get; set; }
        public List<string> ?joinAudiosList { get; set; }
        public static BotParameters LoadFromJson(string filePath)
        {
            if (!File.Exists(filePath))
                throw new ParametersFileNotFound();

            var json = File.ReadAllText(filePath);
            try
            {
                return JsonConvert.DeserializeObject<BotParameters>(json)!;
            }
            catch
            {
                throw new InvalidParametersFormat();
            }           
        }

        public bool IsComplete()
        {
            bool incompleteParameters = string.IsNullOrEmpty(discord_token) || string.IsNullOrEmpty(prefix) || perServerQueueLimit == 0;
            string incompleteParametersInfo ="GENERAL\n";
            if (!incompleteParameters)
            {
                incompleteParameters = (lavalinkCredentials == null || string.IsNullOrEmpty(lavalinkCredentials.host) || lavalinkCredentials.port == 0 || string.IsNullOrEmpty(lavalinkCredentials.password) || string.IsNullOrEmpty(lavalinkCredentials.searchEngine));
                incompleteParametersInfo += "LAVALINK\n";
            }

            if (!incompleteParameters && useSpotify)
            {
                incompleteParameters = spotifyCredentials == null || string.IsNullOrEmpty(spotifyCredentials.clientId) || string.IsNullOrEmpty(spotifyCredentials.clientSecret);
                incompleteParametersInfo += "SPOTIFY\n";
            }

            if (!incompleteParameters && allowJoinAudio)
            {
                incompleteParameters = joinAudiosList == null || joinAudiosList.Count == 0;
                incompleteParametersInfo += "JOIN AUDIOS";
            }

            if (incompleteParameters)
                throw new IncompleteParameters("Parameters not found or not valid in at least module(s): "+incompleteParametersInfo);
            else
                return true;
        }

        public static string GetAppSettingsPath()
        {
            return "applications.json";
        }   

        public static string GetMessagesPath()
        {
            return "messages.json";
        }


    }


}
