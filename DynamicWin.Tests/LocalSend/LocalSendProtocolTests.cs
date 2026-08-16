using DynamicWin.LocalSend;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DynamicWin.Tests.LocalSend;

public class LocalSendModelsTests
{
    [Fact]
    public void Announcement_ParseSpecExample_ReadsAllFields()
    {
        var json = """
        {
          "alias": "Nice Orange",
          "version": "2.0",
          "deviceModel": "Samsung",
          "deviceType": "mobile",
          "fingerprint": "abc123fingerprint",
          "port": 53317,
          "protocol": "https",
          "download": true,
          "announce": true
        }
        """;

        var identity = DeviceIdentity.FromJson(json);

        Assert.NotNull(identity);
        Assert.Equal("Nice Orange", identity.Alias);
        Assert.Equal("2.0", identity.Version);
        Assert.Equal("Samsung", identity.DeviceModel);
        Assert.Equal(LocalSendDeviceKind.Mobile, identity.Kind);
        Assert.Equal("abc123fingerprint", identity.Fingerprint);
        Assert.Equal(53317, identity.Port);
        Assert.Equal("https", identity.Protocol);
        Assert.True(identity.Download);
        Assert.True(identity.Announce);
    }

    [Fact]
    public void DeviceType_UnknownValue_FallsBackToDesktop()
    {
        Assert.Equal(LocalSendDeviceKind.Desktop, DeviceIdentity.ParseDeviceKind("smartwatch"));
        Assert.Equal(LocalSendDeviceKind.Desktop, DeviceIdentity.ParseDeviceKind(null));
        Assert.Equal(LocalSendDeviceKind.Headless, DeviceIdentity.ParseDeviceKind("headless"));
        Assert.Equal(LocalSendDeviceKind.Server, DeviceIdentity.ParseDeviceKind("server"));
    }

    [Fact]
    public void Identity_MissingPort_UsesDefault()
    {
        var identity = DeviceIdentity.FromJson("""{"alias":"A","fingerprint":"f"}""");

        Assert.NotNull(identity);
        Assert.Equal(LocalSendProtocol.DefaultHttpPort, identity.EffectivePort);
    }

    [Fact]
    public void Announcement_Serialize_UsesSpecFieldNames()
    {
        var identity = new DeviceIdentity
        {
            Alias = "PC",
            Fingerprint = "fp",
            Port = 53317,
            Protocol = "http",
            DeviceModel = "Windows",
            DeviceType = "desktop",
            Announce = true,
        };

        var obj = JObject.Parse(identity.ToJson());

        Assert.Equal("PC", obj["alias"]?.ToString());
        Assert.Equal("fp", obj["fingerprint"]?.ToString());
        Assert.Equal(53317, obj["port"]?.Value<int>());
        Assert.Equal("http", obj["protocol"]?.ToString());
        Assert.True(obj["announce"]?.Value<bool>());
    }

    [Fact]
    public void PrepareUploadRequest_Serialize_MatchesSpecShape()
    {
        var request = new PrepareUploadRequest();
        request.Info = new DeviceIdentity { Alias = "PC", Fingerprint = "fp", Port = 53317, Protocol = "http" };
        request.Files["0"] = new FileMetadata { Id = "0", FileName = "photo.jpg", Size = 1234, FileType = "image/jpeg" };

        var obj = JObject.Parse(request.ToJson());

        Assert.Equal("PC", obj["info"]?["alias"]?.ToString());
        Assert.Equal(53317, obj["info"]?["port"]?.Value<int>());
        var file = obj["files"]?["0"];
        Assert.NotNull(file);
        Assert.Equal("photo.jpg", file?["fileName"]?.ToString());
        Assert.Equal(1234, file?["size"]?.Value<long>());
        Assert.Equal("image/jpeg", file?["fileType"]?.ToString());
    }

    [Fact]
    public void PrepareUploadResponse_ParseSpecExample()
    {
        var payload = PrepareUploadResponse.FromJson("""{"sessionId":"mySessionId","files":{"someFileId":"someFileToken"}}""");

        Assert.NotNull(payload);
        Assert.Equal("mySessionId", payload!.SessionId);
        Assert.Equal("someFileToken", payload.Files["someFileId"]);
    }

    [Theory]
    [InlineData("x.PNG", "image/png")]
    [InlineData("a.b.jpg", "image/jpeg")]
    [InlineData("doc.pdf", "application/pdf")]
    [InlineData("song.MP3", "audio/mpeg")]
    [InlineData("weird.xyz", "application/octet-stream")]
    [InlineData("noext", "application/octet-stream")]
    public void MimeFor_CommonTypes(string fileName, string expected)
    {
        Assert.Equal(expected, LocalSendProtocol.MimeFor(fileName));
    }
}

public class LocalSendDeviceRegistryTests
{
    [Fact]
    public void AddOrUpdate_SameFingerprint_ReplacesAddress()
    {
        var registry = new LocalSendDeviceRegistry();
        var identity = new DeviceIdentity { Alias = "Phone", Fingerprint = "fp1", Port = 53317, Protocol = "http" };

        registry.AddOrUpdate(identity, "192.168.1.10");
        registry.AddOrUpdate(identity, "192.168.1.11");

        var device = Assert.Single(registry.Snapshot());
        Assert.Equal("192.168.1.11", device.Address);
    }

    [Fact]
    public void AddOrUpdate_EmptyFingerprint_Ignored()
    {
        var registry = new LocalSendDeviceRegistry();

        registry.AddOrUpdate(new DeviceIdentity { Alias = "X" }, "1.2.3.4");

        Assert.Empty(registry.Snapshot());
    }

    [Fact]
    public void Prune_RemovesStaleDevices_KeepsFresh()
    {
        var time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var registry = new LocalSendDeviceRegistry(() => time);

        registry.AddOrUpdate(new DeviceIdentity { Alias = "Old", Fingerprint = "old" }, "1.1.1.1");
        time = time.AddSeconds(100);
        registry.AddOrUpdate(new DeviceIdentity { Alias = "New", Fingerprint = "new" }, "2.2.2.2");

        registry.Prune(time.AddSeconds(1), TimeSpan.FromSeconds(60));

        var device = Assert.Single(registry.Snapshot());
        Assert.Equal("new", device.Identity.Fingerprint);
    }

    [Fact]
    public void Snapshot_SortedByAlias()
    {
        var registry = new LocalSendDeviceRegistry();
        registry.AddOrUpdate(new DeviceIdentity { Alias = "Zeta", Fingerprint = "z" }, "1.1.1.1");
        registry.AddOrUpdate(new DeviceIdentity { Alias = "Alpha", Fingerprint = "a" }, "2.2.2.2");

        var snapshot = registry.Snapshot();

        Assert.Equal("Alpha", snapshot[0].Identity.Alias);
        Assert.Equal("Zeta", snapshot[1].Identity.Alias);
    }
}
