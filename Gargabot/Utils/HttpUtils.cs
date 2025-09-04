using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Gargabot.Utils
{
    public static class HttpUtils
    {
        public static async Task<string> ResolveRedirect(string url)
        {
            try
            {
                var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = false
                };

                using var client = new HttpClient(handler);
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
                {
                    var location = response.Headers.Location;
                    if (location != null)
                    {
                        return new Uri(new Uri(url), location).ToString();
                    }
                }
                else if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
                {
                    return url;
                }
            }
            catch { }

            return "";
        }
    }
}
