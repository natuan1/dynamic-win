using DynamicWin.Main;
using DynamicWin.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace DynamicWin.LocalSend;

public enum LocalSendSendStatus { Idle, Preparing, Sending, Completed, Rejected, RequiresPin, Busy, Failed, Cancelled }

public sealed class LocalSendService(ISettingsStore settingsStore) : IApplicationComponent
{
    public static LocalSendService? Instance { get; private set; }

    // Diagnostics only; safe to delete the file at any time.
    public static readonly string LogPath = Path.Combine(Path.GetTempPath(), "DynamicWin.LocalSend.log");

    static readonly object logGate = new();
    static void Log(string message)
    {
        try
        {
            lock (logGate)
                File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch { }
    }

    private readonly LocalSendDeviceRegistry registry = new();
    public LocalSendDeviceRegistry Registry => registry;

    public DeviceIdentity Self { get; private set; } = new();
    public int HttpPort { get; private set; } = LocalSendProtocol.DefaultHttpPort;

    private readonly object stateGate = new();
    private LocalSendSendStatus status = LocalSendSendStatus.Idle;
    private string statusMessage = "";
    private float progress;
    private string currentFile = "";
    private CancellationTokenSource? sendCts;
    private string? activeSessionId;

    private HttpClient? http;
    private Socket? udp;
    private CancellationTokenSource? networkCts;
    private WebApplication? app;

    public LocalSendSendStatus Status { get { lock (stateGate) return status; } }
    public string StatusMessage { get { lock (stateGate) return statusMessage; } }
    public float Progress { get { lock (stateGate) return progress; } }
    public string CurrentFile { get { lock (stateGate) return currentFile; } }

    public IReadOnlyList<LocalSendDevice> Devices => registry.Snapshot();

    public void Start()
    {
        if (!RegisterLocalSendSettings.saveData.enabled) return;

        Instance = this;

        EnsureFirewallRule();

        var fingerprint = settingsStore.Get<string>("localsend.fingerprint");
        if (string.IsNullOrEmpty(fingerprint))
        {
            fingerprint = Guid.NewGuid().ToString("N");
            settingsStore.Set("localsend.fingerprint", fingerprint);
            settingsStore.Save();
        }

        Self = new DeviceIdentity
        {
            Alias = Environment.MachineName,
            Version = LocalSendProtocol.ProtocolVersion,
            DeviceModel = "Windows",
            DeviceType = "desktop",
            Fingerprint = fingerprint,
            Protocol = "http",
            Download = false,
        };

        http = new HttpClient(new HttpClientHandler
        {
            // LocalSend peers use self-signed certificates; fingerprint pinning is a
            // follow-up (see docs/adr/0001).
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        })
        { Timeout = Timeout.InfiniteTimeSpan };
        networkCts = new CancellationTokenSource();

        StartHttpServer();
        Log($"Started: alias={Self.Alias} fingerprint={Self.Fingerprint} httpPort={HttpPort}");

        udp = CreateMulticastSocket();
        Log(udp.LocalEndPoint is IPEndPoint { Port: LocalSendProtocol.MulticastPort }
            ? "Multicast socket joined 224.0.0.167:53317"
            : $"Multicast fallback socket ({udp.LocalEndPoint})");

        StartNetworkLoops();
    }

    void EnsureFirewallRule()
    {
        try
        {
            if (settingsStore.Get<bool>("localsend.firewall.declined")) return;
        }
        catch { }

        if (LocalSendFirewall.EnsureRule()) return;

        Log("Firewall install declined by user; saving localsend.firewall.declined");
        try
        {
            settingsStore.Set("localsend.firewall.declined", true);
            settingsStore.Save();
        }
        catch { }
    }

    public void Stop()
    {
        networkCts?.Cancel();
        udp?.Dispose();
        udp = null;

        lock (stateGate)
        {
            sendCts?.Cancel();
        }

        var application = app;
        app = null;
        if (application != null)
        {
            try { application.StopAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult(); } catch { }
            try { application.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
        }

        http?.Dispose();
        http = null;

        if (ReferenceEquals(Instance, this))
            Instance = null;
    }

    void StartHttpServer()
    {
        foreach (var candidate in LocalSendPortPicker.Candidates)
        {
            try
            {
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
                builder.Logging.ClearProviders();
                builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Any, candidate));

                var application = builder.Build();

                // DeviceIdentity uses Newtonsoft attributes on public fields, which the
                // minimal-API System.Text.Json binder cannot map — parse the body manually.
                application.MapPost(LocalSendProtocol.ApiBase + "/register", async (HttpContext context) =>
                {
                    using var reader = new StreamReader(context.Request.Body);
                    var registration = DeviceIdentity.FromJson(await reader.ReadToEndAsync());

                    if (registration?.Fingerprint is { Length: > 0 } fingerprint && fingerprint != Self.Fingerprint)
                    {
                        Log($"Register from {registration.Alias} ({fingerprint}) via {context.Connection.RemoteIpAddress}, port={registration.EffectivePort} protocol={registration.Protocol}");
                        registry.AddOrUpdate(registration, context.Connection.RemoteIpAddress?.ToString() ?? "");
                    }
                    return Results.Json(Self.ToInfoResponse());
                });

                application.MapGet(LocalSendProtocol.ApiBase + "/info", () => Results.Json(Self.ToInfoResponse()));

                application.StartAsync().GetAwaiter().GetResult();

                app = application;
                HttpPort = ResolveBoundPort(application);
                Self.Port = HttpPort;
                return;
            }
            catch
            {
                try { app?.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
                app = null;
            }
        }
    }

    static int ResolveBoundPort(WebApplication application)
    {
        var addresses = application.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()?.Addresses;

        if (addresses != null)
        {
            foreach (var address in addresses)
            {
                var separator = address.LastIndexOf(':');
                if (separator >= 0 && int.TryParse(address[(separator + 1)..], out var port))
                    return port;
            }
        }

        return LocalSendProtocol.DefaultHttpPort;
    }

    Socket CreateMulticastSocket()
    {
        try
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Bind(new IPEndPoint(IPAddress.Any, LocalSendProtocol.MulticastPort));
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                new MulticastOption(IPAddress.Parse(LocalSendProtocol.MulticastAddress)));
            return socket;
        }
        catch
        {
            // Port 53317 UDP unavailable (e.g. another exclusive socket): fall back to
            // send-only so announcements still go out, though we cannot hear others.
            return new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        }
    }

    void StartNetworkLoops()
    {
        var token = networkCts!.Token;
        var client = udp!;

        _ = Task.Run(async () =>
        {
            var endpoint = new IPEndPoint(IPAddress.Parse(LocalSendProtocol.MulticastAddress), LocalSendProtocol.MulticastPort);
            var announcement = Self.Clone();
            announcement.Announce = true;
            var payload = Encoding.UTF8.GetBytes(announcement.ToJson());
            var joined = new HashSet<IPAddress>();

            while (!token.IsCancellationRequested)
            {
                // Windows routes multicast through one default interface (a VPN
                // adapter here would swallow it), so announce and listen on
                // every live IPv4 interface instead.
                var interfaces = GetIpv4InterfaceAddresses();
                Log($"Announcing on: {(interfaces.Count > 0 ? string.Join(", ", interfaces) : "no IPv4 interface")}");

                foreach (var local in interfaces)
                {
                    if (joined.Add(local))
                    {
                        try { client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(IPAddress.Parse(LocalSendProtocol.MulticastAddress), local)); } catch { }
                    }

                    try
                    {
                        client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, local.GetAddressBytes());
                        client.SendTo(payload, SocketFlags.None, endpoint);
                    }
                    catch { }
                }

                registry.Prune(DateTime.UtcNow, TimeSpan.FromSeconds(60));

                try { await Task.Delay(TimeSpan.FromSeconds(10), token); } catch { }
            }
        }, token);

        _ = Task.Run(async () =>
        {
            try { await Task.Delay(TimeSpan.FromSeconds(5), token); } catch { return; }

            while (!token.IsCancellationRequested)
            {
                try { await ScanSubnetAsync(token); } catch { }
                try { await Task.Delay(TimeSpan.FromSeconds(45), token); } catch { }
            }
        }, token);

        // Receiving announcements only works on the shared multicast port; the
        // send-only fallback socket would receive nothing.
        if (client.LocalEndPoint is IPEndPoint { Port: LocalSendProtocol.MulticastPort })
        {
            _ = Task.Run(() =>
            {
                var buffer = new byte[8192];
                while (!token.IsCancellationRequested)
                {
                    int received;
                    EndPoint source = new IPEndPoint(IPAddress.Any, 0);
                    try
                    {
                        received = client.ReceiveFrom(buffer, ref source);
                    }
                    catch (SocketException) { break; }
                    catch (ObjectDisposedException) { break; }

                    if (received <= 0) continue;
                    HandleDatagram(buffer, received, (IPEndPoint)source);
                }
            });
        }
    }

    static List<IPAddress> GetIpv4InterfaceAddresses()
    {
        var result = new List<IPAddress>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    result.Add(unicast.Address);
                    break;
                }
            }
        }
        catch { }

        return result;
    }

    // Fallback discovery for networks where multicast does not cross between
    // WiFi and wired segments: probe the /24 of every local address for the
    // LocalSend /info endpoint, over both https and http.
    async Task ScanSubnetAsync(CancellationToken token)
    {
        foreach (var local in GetIpv4InterfaceAddresses())
        {
            var bytes = local.GetAddressBytes();
            var prefix = $"{bytes[0]}.{bytes[1]}.{bytes[2]}";

            var probes = Enumerable.Range(1, 254).Select(host => ProbeAddressAsync($"{prefix}.{host}", token));
            try { await Task.WhenAll(probes); } catch { }
            Log($"Scan pass done for {prefix}.0/24");
        }
    }

    async Task ProbeAddressAsync(string address, CancellationToken token)
    {
        if (http == null) return;

        foreach (var scheme in new[] { "https", "http" })
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromMilliseconds(900));
                var json = await http.GetStringAsync(
                    $"{scheme}://{address}:{LocalSendProtocol.DefaultHttpPort}{LocalSendProtocol.ApiBase}/info", timeout.Token);

                var identity = DeviceIdentity.FromJson(json);
                if (string.IsNullOrEmpty(identity?.Fingerprint) || identity.Fingerprint == Self.Fingerprint) return;

                identity.Port = LocalSendProtocol.DefaultHttpPort;
                identity.Protocol = scheme;
                registry.AddOrUpdate(identity, address);
                Log($"Scan found {identity.Alias} ({identity.Fingerprint}) at {address} via {scheme}");
                return;
            }
            catch { }
        }
    }

    void HandleDatagram(byte[] buffer, int length, IPEndPoint remote)
    {
        var identity = DeviceIdentity.FromJson(Encoding.UTF8.GetString(buffer, 0, length));
        if (identity == null || identity.Fingerprint == Self.Fingerprint) return;

        Log($"Datagram from {identity.Alias} ({identity.Fingerprint}) at {remote.Address}, port={identity.EffectivePort} protocol={identity.Protocol} announce={identity.Announce}");

        registry.AddOrUpdate(identity, remote.Address.ToString() ?? "");

        if (identity.Announce == true)
            _ = RegisterToDeviceAsync(remote.Address.ToString() ?? "", identity.EffectivePort, networkCts!.Token);
    }

    async Task RegisterToDeviceAsync(string address, int port, CancellationToken token)
    {
        if (http == null) return;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await http.PostAsync(
                $"http://{address}:{port}{LocalSendProtocol.ApiBase}/register",
                new StringContent(Self.ToJson(), Encoding.UTF8, "application/json"),
                timeout.Token);
        }
        catch { }
    }

    public void SendFiles(LocalSendDevice device, IReadOnlyList<string> filePaths)
    {
        CancellationTokenSource cts;

        lock (stateGate)
        {
            if (status is LocalSendSendStatus.Preparing or LocalSendSendStatus.Sending) return;

            sendCts = new CancellationTokenSource();
            cts = sendCts;
        }

        _ = SendFilesAsync(device, filePaths, cts.Token);
    }

    public void CancelSend()
    {
        CancellationTokenSource? cts;
        string? session;

        lock (stateGate)
        {
            cts = sendCts;
            session = activeSessionId;
        }

        cts?.Cancel();

        if (session != null && http != null && sessionTarget != null)
            _ = Task.Run(async () =>
            {
                try
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await http.PostAsync(
                        $"{DeviceBaseUrl(sessionTarget)}{LocalSendProtocol.ApiBase}/cancel?sessionId={Uri.EscapeDataString(session)}",
                        null, timeout.Token);
                }
                catch { }
            });
    }

    static string DeviceBaseUrl(LocalSendDevice device) =>
        $"{(device.Identity.Protocol == "https" ? "https" : "http")}://{device.Address}:{device.Identity.EffectivePort}";

    LocalSendDevice? sessionTarget;

    async Task SendFilesAsync(LocalSendDevice device, IReadOnlyList<string> filePaths, CancellationToken ct)
    {
        var alias = device.Identity.Alias;

        try
        {
            Log($"Send begin: target={alias} at {DeviceBaseUrl(device)}, files=[{string.Join(", ", filePaths)}]");

            SetState(LocalSendSendStatus.Preparing, $"Waiting for {alias} to accept…", 0, "");

            var files = filePaths.Where(File.Exists).ToList();
            if (files.Count == 0)
            {
                Log("Send aborted: no existing files");
                SetState(LocalSendSendStatus.Failed, "No files to send", 0, "");
                return;
            }

            var request = new PrepareUploadRequest { Info = Self.Clone() };
            for (var i = 0; i < files.Count; i++)
            {
                var info = new FileInfo(files[i]);
                request.Files[i.ToString()] = new FileMetadata
                {
                    Id = i.ToString(),
                    FileName = Path.GetFileName(files[i]),
                    Size = info.Length,
                    FileType = LocalSendProtocol.MimeFor(files[i]),
                };
            }

            using var prepareTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            prepareTimeout.CancelAfter(TimeSpan.FromSeconds(20));

            using var prepareResponse = await http!.PostAsync(
                $"{DeviceBaseUrl(device)}{LocalSendProtocol.ApiBase}/prepare-upload",
                new StringContent(request.ToJson(), Encoding.UTF8, "application/json"),
                prepareTimeout.Token);

            if (prepareResponse.StatusCode == HttpStatusCode.NoContent)
            {
                SetState(LocalSendSendStatus.Completed, $"Nothing left to send to {alias}", 1, "");
                return;
            }
            if (prepareResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                SetState(LocalSendSendStatus.RequiresPin, $"{alias} requires a PIN", 0, "");
                return;
            }
            if (prepareResponse.StatusCode == HttpStatusCode.Forbidden)
            {
                SetState(LocalSendSendStatus.Rejected, $"{alias} rejected the transfer", 0, "");
                return;
            }
            if (prepareResponse.StatusCode == HttpStatusCode.Conflict)
            {
                SetState(LocalSendSendStatus.Busy, $"{alias} is busy with another transfer", 0, "");
                return;
            }
            if (!prepareResponse.IsSuccessStatusCode)
            {
                Log($"Prepare-upload failed: {(int)prepareResponse.StatusCode} {prepareResponse.ReasonPhrase}");
                SetState(LocalSendSendStatus.Failed, $"{alias} responded with {(int)prepareResponse.StatusCode}", 0, "");
                return;
            }

            var payload = PrepareUploadResponse.FromJson(await prepareResponse.Content.ReadAsStringAsync(prepareTimeout.Token));
            if (payload == null || payload.Files.Count == 0)
            {
                Log($"Prepare-upload accepted no files, body=({(payload == null ? "unparseable" : "empty file list")})");
                SetState(LocalSendSendStatus.Rejected, $"{alias} accepted none of the files", 0, "");
                return;
            }

            Log($"Prepare-upload ok: sessionId={payload.SessionId}, {payload.Files.Count} file(s) accepted");

            lock (stateGate)
            {
                activeSessionId = payload.SessionId;
                sessionTarget = device;
            }

            long totalBytes = 0;
            foreach (var entry in payload.Files)
                if (request.Files.TryGetValue(entry.Key, out var metadata))
                    totalBytes += metadata.Size;

            long sentBytes = 0;
            var sentCount = 0;

            foreach (var entry in request.Files)
            {
                if (!payload.Files.TryGetValue(entry.Key, out var token)) continue;

                var metadata = entry.Value;
                var fileName = metadata.FileName;
                SetState(LocalSendSendStatus.Sending, "", sentBytes / (float)Math.Max(totalBytes, 1), fileName);

                using var content = new ProgressFileContent(files[int.Parse(entry.Key)], metadata.Size, bytesRead =>
                {
                    lock (stateGate)
                    {
                        sentBytes += bytesRead;
                        progress = sentBytes / (float)Math.Max(totalBytes, 1);
                    }
                });

                using var uploadResponse = await http.PostAsync(
                    $"{DeviceBaseUrl(device)}{LocalSendProtocol.ApiBase}/upload?sessionId={Uri.EscapeDataString(payload.SessionId)}&fileId={Uri.EscapeDataString(entry.Key)}&token={Uri.EscapeDataString(token)}",
                    content, ct);

                if (!uploadResponse.IsSuccessStatusCode)
                {
                    Log($"Upload failed for {fileName}: {(int)uploadResponse.StatusCode} {uploadResponse.ReasonPhrase}");
                    SetState(LocalSendSendStatus.Failed, $"{alias} upload failed ({(int)uploadResponse.StatusCode}): {fileName}", 0, fileName);
                    return;
                }

                Log($"Uploaded {fileName}");
                sentCount++;
            }

            lock (stateGate) activeSessionId = null;

            Log($"Send completed: {sentCount} file(s) to {alias}");
            SetState(LocalSendSendStatus.Completed, $"Sent {sentCount} file(s) to {alias}", 1, "");
        }
        catch (OperationCanceledException)
        {
            Log("Send cancelled");
            SetState(LocalSendSendStatus.Cancelled, "Cancelled", 0, "");
        }
        catch (Exception exception)
        {
            Log($"Send error: {exception}");
            SetState(LocalSendSendStatus.Failed, $"Error: {exception.Message}", 0, "");
        }
    }

    void SetState(LocalSendSendStatus newStatus, string message, float overallProgress, string file)
    {
        lock (stateGate)
        {
            status = newStatus;
            statusMessage = message;
            progress = overallProgress;
            currentFile = file;
        }
    }
}

sealed class ProgressFileContent : HttpContent
{
    private readonly string path;
    private readonly long length;
    private readonly Action<long> onBytes;

    public ProgressFileContent(string path, long length, Action<long> onBytes)
    {
        this.path = path;
        this.length = length;
        this.onBytes = onBytes;
        Headers.ContentLength = length;
    }

    protected override bool TryComputeLength(out long computedLength)
    {
        computedLength = length;
        return true;
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        var buffer = new byte[81920];
        await using var file = File.OpenRead(path);
        int read;
        while ((read = await file.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            await stream.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
            onBytes(read);
        }
    }
}
