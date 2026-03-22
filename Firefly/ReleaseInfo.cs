using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Firefly;

public partial class ReleaseInfo
{


    [JsonPropertyName("version")]
    public string Version { get; set; }


    [JsonPropertyName("time")]
    public DateTimeOffset Time { get; set; }


    [JsonPropertyName("link")]
    public string Link { get; set; }


    [JsonSerializable(typeof(ReleaseInfo))]
    public partial class JsonContext : JsonSerializerContext { }


    public static async Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellation = default)
    {
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All, UseProxy = false });
            client.DefaultRequestHeaders.Add("User-Agent", $"Firefly/{AppSetting.AppVersion}");
            client.DefaultRequestHeaders.Add("X-Device-Id", AppSetting.DeviceId.ToString());
            client.DefaultRequestHeaders.Add("X-Session-Id", AppSetting.SessionId.ToString());
            string content = await client.GetStringAsync("https://firefly.scighost.com/release/latest.json", cancellation);
            return JsonSerializer.Deserialize<ReleaseInfo?>(content, JsonContext.Default.ReleaseInfo);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return null;
        }
    }

}
