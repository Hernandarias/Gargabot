# Gargabot

Gargabot is a music playing bot for Discord servers built using DSharpPlus in .NET 8


## Features

- Ability to play tracks from YouTube and Spotify
- Ability to customize several tracks to be played when the bot joins a voice channel
- Low RAM and CPU usage
- On-going operations limit to avoid excessive resource usage
- Multi-server support
- Search and queue management
- Playback control
- Fully customizable parameters like the bot's prefix, playback method, etc
- Fully customizable messages

## Commands (the prefix is customizable)

```
g!play <[!music]> <search>: Search and play a track. Use [!music] before the search query to specify searching from YouTube Music. If nothing specified, it searches from YouTube. If the query is a link, it can be a YouTube link (video or playlist) or a Spotify link (song, album, or playlist). Any other links will be handled by Lavaplayer if chosen as the playback method.
g!pause: Pause the current playback.
g!resume: Resume playback.
g!skip: Skip the current song.
g!stop: Stop playback.
g!queue: Show the playback queue.
g!remove <index>: Remove an item from the queue.
g!clear: Remove all items from the queue.
g!shuffle: Shuffle the playback queue.
g!help: Show the help message.
```

## Setting Gargabot up

### Requirements:

- [.NET 8](https://dotnet.microsoft.com/es-es/download/dotnet/8.0)
- Either [Lavalink 3.7.11](https://github.com/lavalink-devs/Lavalink/releases/tag/3.7.11) to use Lavalink as playback method or both [ffmpeg](https://ffmpeg.org/download.html) and [yt-dlp](https://github.com/yt-dlp/yt-dlp/releases) to use VoiceNext

### Choosing a playback method:

Both VoiceNext and Lavalink are available on Gargabot to be used to play audio in voice channels. However, Gargabot's VoiceNext module is deprecated and won't be getting any updates.

#### Comparison chart:

| Method | External apps needed | YouTube search support| YouTube Music search support | Spotify search support | Queue limit | On-going operations limit |
|------------------|--------------|------------|---------|-----------|------| ----- |
| Lavalink            | 1       | Videos and playlists (both from links and text queries)         | Text queries       | Songs, playlists and albums links         | Yes    | No   | 
| VoiceNext            | 2          | Only videos (both from links and text queries)         | No       | No         | No    | No |


### Configuration files:

- applications.json:
```json
{
  "discord_token": "YOUR_DISCORD_TOKEN", #Your Discord bot token
  "prefix": "g!", #This customizes every Gargabot command prefix
  "ffmpeg_path": "YOUR_FFMPEG.EXE_PATH",
  "yt_dlp_path": "YOUR_YT_DLP.EXE_PATH",
  "lavalinkOrVoiceNext": "lavalink", #Options are "lavalink" or "voicenext". If "lavalink" is chosen then there's no need to fill in the previous two variables (ffmpeg_path and yt_dlp_path)
  "perServerQueueLimit": 50, #Per-server queue limit
  "lavalinkCredentials": { #There's no need to fill this data if "voicenext". This data is customizable in Lavalink's application.yml
    "host": "127.0.0.1",
    "port": 7717,
    "password": "YOUR_LAVALINK_PASSWORD",
    "searchEngine": "youtube" #Options are "youtube" and "soundcloud". "youtube" is heavily recommended as it was entirely recoded in Gargabot's source code
  },
  "useSpotify": true, #Add the ability to play tracks from Spotify links (either tracks, playlists or albums)
  "spotifyCredentials": { #Your Spotify API credentials
    "clientId": "YOUR_CLIENT_ID",
    "clientSecret": "YOUR_CLIENT_SECRET"
  },
  "allowJoinAudio": true, #This allows Gargabot to play a random track from the list below when it joins a voice channel
  "joinAudiosList": [
    "link-1",
    "link-2"
  ]
}
```

- messages.json: (these are the messages sent by the bot when certain actions are triggered)

```json
{
  "NOT_IN_A_VOICE_CHANNEL": "❎ You are not currently in a voice channel.",
  "GET_LAVALINK_CONNECTION_ERROR": "❎ Error connecting to the Lavalink server.",
  "BOT_ALREADY_IN_ANOTHER_VOICE_CHANNEL": "❎ Gargabot is already in another voice channel.",
  "NO_TRACKS_FOUND_FOR_SEARCH": "❎ Nothing found for the search '{0}'.",
  "ADDED_TO_QUEUE_IN_POSITION": "✅ Added to the queue in position {0}.",
  "PLAYING_ON": "Currently playing on the server '{0}'.",
  "NO_AUDIO_PLAYING": "❎ There is nothing playing at the moment.",
  "PAUSED": "✅ Playback paused.",
  "RESUMED": "✅ Playback resumed.",
  "SKIPPED": "✅ Track skipped.",
  "STOPPED": "✅ Playback stopped.",
  "DELETED": "✅ Track '{0}' deleted.",
  "NO_ELEMENTS_IN_QUEUE": "❎ No elements found in the queue.",
  "OUT_OF_RANGE_IN_QUEUE": "❎ Index out of range in the queue.",
  "SHUFFLED": "✅ Queue shuffled.",
  "MULTIPLE_TRACKS_ADDED_TO_QUEUE": "✅ {0} tracks added to the queue.",
  "CLEARED": "✅ Queue cleared.",
  "HELP": "**List of commands:**\n\n{0}play <[!music]> <search> - Search and play a song.\n{0}pause - Pause the current playback.\n{0}resume - Resume playback.\n{0}skip - Skip the current song.\n{0}stop - Stop playback.\n{0}queue - Show the playback queue.\n{0}remove <index> - Remove an item from the queue.\n{0}clear - Remove all items from the queue.\n{0}shuffle - Shuffle the playback queue.\n{0}help - Show this help message.",
  "HEAVY_OPERATION_ONGOING": "❎ AN operation is already taking place. Please wait for it to finish.",
  "QUEUE_LIMIT_REACHED": "❎ The queue limit has been reached."
}

```

Same file in Spanish:

```json
{
  "NOT_IN_A_VOICE_CHANNEL": "❎ No estás actualmente en un canal de voz.",
  "GET_LAVALINK_CONNECTION_ERROR": "❎ Error al conectar con el servidor Lavalink.",
  "BOT_ALREADY_IN_ANOTHER_VOICE_CHANNEL": "❎ Gargabot ya está en otro canal de voz.",
  "NO_TRACKS_FOUND_FOR_SEARCH": "❎ No se encontró nada para la búsqueda \"{0}\".",
  "ADDED_TO_QUEUE_IN_POSITION": "✅ Agregado a la cola en la posición {0}.",
  "PLAYING_ON": "Actualmente reproduciendo en {0}.",
  "NO_AUDIO_PLAYING": "❎ No hay nada reproduciéndose en este momento.",
  "PAUSED": "✅ Reproducción pausada.",
  "RESUMED": "✅ Reproducción reanudada.",
  "SKIPPED": "✅ Track skipeado.",
  "STOPPED": "✅ Reproducción detenida.",
  "DELETED": "✅ Track \"{0}\" eliminado.",
  "NO_ELEMENTS_IN_QUEUE": "❎ No se encontraron elementos en la cola.",
  "OUT_OF_RANGE_IN_QUEUE": "❎ Índice fuera de rango en la cola.",
  "SHUFFLED": "✅ Cola mezclada.",
  "MULTIPLE_TRACKS_ADDED_TO_QUEUE": "✅ {0} elementos agregados a la cola.",
  "CLEARED": "✅ Todos los elementos han sido eliminados.",
  "HELP": "**Listado de comandos:**\n\n`{0}play <[!music]> <búsqueda>` - Busca y reproduce una canción.\n`{0}pause` - Pausa la reproducción actual.\n`{0}resume` - Reanuda la reproducción.\n`{0}skip` - Salta la canción actual.\n`{0}stop` - Detiene la reproducción.\n`{0}queue` - Muestra la cola de reproducción.\n`{0}remove <índice>` - Elimina un elemento de la cola.\n`{0}clear` - Elimina todos los elementos de la cola.\n`{0}shuffle` - Mezcla la cola de reproducción.\n`{0}help` - Muestra este mensaje de ayuda.",
  "HEAVY_OPERATION_ONGOING": "❎ Ya hay una operación en curso. Por favor, espera a que termine.",
  "QUEUE_LIMIT_REACHED": "❎ Se ha alcanzado el límite de la cola."
}
```

### Executing Gargabot:

Once the configuration files were customized you can run Gargabot by running the `Gargabot.exe` executable. Note that if Lavalink is chosen as the playback method it should be running on the credentials specified on the `applications.json` file before executing Gargabot.

## How does Gargabot work?

### Discord integration:

This is done entirely by using the [DSharpPlus](https://github.com/DSharpPlus/DSharpPlus/) library in its latest version. More information on it can be found in its [documentation](https://dsharpplus.github.io/DSharpPlus/)

### Audio playback:

There are two ways of playing audio on Gargabot:

- VoiceNext `(deprecated)`: This uses the [DSharpPlus.VoiceNext](https://www.nuget.org/packages/DSharpPlus.VoiceNext/) package for playing audio in a voice channel. It requires an external app [(ffmpeg)](https://ffmpeg.org/download.html) to actually play the audio and another app to get working streaming links [(yt-dlp)](https://github.com/yt-dlp/yt-dlp/releases). This approach is pretty complicated to mantain and, therefore, won't be getting any fixes or updates.

- Lavalink: This uses the [DSharpPlus.Lavalink](https://www.nuget.org/packages/DSharpPlus.Lavalink/) package for connecting to Lavalink from .NET. Although this package is now deprecated it stills does the job since Lavalink is only used to play audio and nothing else (unless `soundcloud` is chosen as the search engine which, as previously stated, won't be getting any support as it relies entirely on Lavaplayer's implementation of it).

### Spotify, YouTube and YouTube Music integration:

When the `g!play` command is triggered by an user Gargabot checks whether the specified query is a url or plain text.

- If the query is plain text and the `[!music]` clause wasn't used then a search in YouTube is executed using the [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode) package. The first result is the one getting played.
- If the query is plain text and the `[!music]` clause was used then we search that track in YouTube Music using reverse engineering to get as close as we can to the user's request. Note that this assumes the track exists in YouTube Music as a song and not as a music or lyrics video; also, there's no guarantee that the gotten result is a perfect match to the user's requst.
- If the query is a YouTube video or playlist url then [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode) is used to handle those requests.
- If the query is a Spotify song, playlist or album url then the [Spotify's public API](https://developer.spotify.com/documentation/web-api) is consumed to get each track information. Once that information was retrieved then Gargabot searches that track using the YouTube Music search module.

## To-do list

- Permissions system
- Apple Music integration
- Deezer integration
- Suggest your own?
## Screenshots

![App Screenshot](https://i.imgur.com/m7nTwZW.jpeg)

![App Screenshot](https://i.imgur.com/dwt5pjg.jpeg)
## Authors

- [@Hernandarias](https://github.com/Hernandarias)


## License

[MIT](https://choosealicense.com/licenses/mit/)
