using Newtonsoft.Json;
using System.IO;

namespace DynamicWin.LocalSend;

public static class LocalSendProtocol
{
    public const string MulticastAddress = "224.0.0.167";
    public const int MulticastPort = 53317;
    public const int DefaultHttpPort = 53317;
    public const string ProtocolVersion = "2.1";
    public const string ApiBase = "/api/localsend/v2";

    public static string MimeFor(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".zip" => "application/zip",
            _ => "application/octet-stream",
        };
    }
}

public enum LocalSendDeviceKind { Mobile, Desktop, Web, Headless, Server }

public class DeviceIdentity
{
    [JsonProperty("alias")] public string Alias = "";
    [JsonProperty("version")] public string Version = LocalSendProtocol.ProtocolVersion;
    [JsonProperty("deviceModel", NullValueHandling = NullValueHandling.Ignore)] public string? DeviceModel;
    [JsonProperty("deviceType", NullValueHandling = NullValueHandling.Ignore)] public string? DeviceType;
    [JsonProperty("fingerprint")] public string Fingerprint = "";
    [JsonProperty("port", NullValueHandling = NullValueHandling.Ignore)] public int? Port;
    [JsonProperty("protocol", NullValueHandling = NullValueHandling.Ignore)] public string? Protocol;
    [JsonProperty("download")] public bool Download;
    [JsonProperty("announce", NullValueHandling = NullValueHandling.Ignore)] public bool? Announce;

    [JsonIgnore] public LocalSendDeviceKind Kind => ParseDeviceKind(DeviceType);

    [JsonIgnore] public int EffectivePort => Port ?? LocalSendProtocol.DefaultHttpPort;

    public static LocalSendDeviceKind ParseDeviceKind(string? deviceType) => deviceType switch
    {
        "mobile" => LocalSendDeviceKind.Mobile,
        "web" => LocalSendDeviceKind.Web,
        "headless" => LocalSendDeviceKind.Headless,
        "server" => LocalSendDeviceKind.Server,
        _ => LocalSendDeviceKind.Desktop,
    };

    public static DeviceIdentity? FromJson(string json) => JsonConvert.DeserializeObject<DeviceIdentity>(json);

    public string ToJson() => JsonConvert.SerializeObject(this, Formatting.None);

    public DeviceIdentity Clone() => new()
    {
        Alias = Alias,
        Version = Version,
        DeviceModel = DeviceModel,
        DeviceType = DeviceType,
        Fingerprint = Fingerprint,
        Port = Port,
        Protocol = Protocol,
        Download = Download,
        Announce = Announce,
    };

    public object ToInfoResponse() => new
    {
        alias = Alias,
        version = Version,
        deviceModel = DeviceModel,
        deviceType = DeviceType,
        fingerprint = Fingerprint,
        download = Download,
    };
}

public class FileMetadata
{
    [JsonProperty("id")] public string Id = "";
    [JsonProperty("fileName")] public string FileName = "";
    [JsonProperty("size")] public long Size;
    [JsonProperty("fileType")] public string FileType = "application/octet-stream";
    [JsonProperty("sha256", NullValueHandling = NullValueHandling.Ignore)] public string? Sha256;
    [JsonProperty("preview", NullValueHandling = NullValueHandling.Ignore)] public string? Preview;
}

public class PrepareUploadRequest
{
    [JsonProperty("info")] public DeviceIdentity Info = new();
    [JsonProperty("files")] public Dictionary<string, FileMetadata> Files = new();

    public string ToJson() => JsonConvert.SerializeObject(this, Formatting.None);
}

public class PrepareUploadResponse
{
    [JsonProperty("sessionId")] public string SessionId = "";
    [JsonProperty("files")] public Dictionary<string, string> Files = new();

    public static PrepareUploadResponse? FromJson(string json) => JsonConvert.DeserializeObject<PrepareUploadResponse>(json);
}
