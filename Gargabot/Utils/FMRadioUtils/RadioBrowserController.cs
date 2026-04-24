using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Gargabot.Utils.FMRadioUtils
{
    public sealed class RadioBrowserStation
    {
        [JsonPropertyName("stationuuid")]
        public string StationUuid { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("url")]
        public string Url { get; set; } = "";

        [JsonPropertyName("url_resolved")]
        public string UrlResolved { get; set; } = "";

        [JsonPropertyName("country")]
        public string Country { get; set; } = "";

        [JsonPropertyName("codec")]
        public string Codec { get; set; } = "";

        [JsonPropertyName("bitrate")]
        public int Bitrate { get; set; }
    }

    public static class RadioBrowserController
    {
        private static readonly HttpClient _http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:108.0) Gecko/20100101 Firefox/108.0");
            return client;
        }

        public static async Task<List<RadioBrowserStation>> SearchStationsByNameAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<RadioBrowserStation>();

            string encoded = Uri.EscapeDataString(query.Trim());

            string url = $"https://de2.api.radio-browser.info/json/stations/byname/{encoded}?hidebroken=true&order=votes&reverse=true&offset=0&limit=10";

            var result = await _http.GetFromJsonAsync<List<RadioBrowserStation>>(url);

            if (result is null)
                return new List<RadioBrowserStation>();

            var filtered = new List<RadioBrowserStation>();
            foreach (var x in result)
            {
                if (!string.IsNullOrWhiteSpace(x.Name) && (!string.IsNullOrWhiteSpace(x.UrlResolved) || !string.IsNullOrWhiteSpace(x.Url)))
                {
                    filtered.Add(x);
                    if (filtered.Count >= 10)
                        break;
                }
            }
            return filtered;
        }

        public static string GetPlayableUrl(RadioBrowserStation station)
        {
            if (!string.IsNullOrWhiteSpace(station.UrlResolved))
            {
                return station.UrlResolved;
            }
            else
            {
                return station.Url;
            }
        }

        public static string BuildRadioDescription(RadioBrowserStation station)
        {
            string description = "";
            if (!string.IsNullOrWhiteSpace(station.Country))
                description += station.Country;
            else
                description += "?";

            if(station.Bitrate > 0)
            {
                if (description != "?")
                    description += " | ";
                description += $"{station.Bitrate} kbps";
            }

            return description;
        }

    }
}
