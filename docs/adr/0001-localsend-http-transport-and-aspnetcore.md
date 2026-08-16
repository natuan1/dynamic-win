# ADR 0001: LocalSend transport — HTTP plaintext for v1, ASP.NET Core server

- Status: Accepted
- Date: 2026-08-16
- Issue: #26

## Context

DynamicWin integrates the [LocalSend Protocol v2](https://github.com/localsend/protocol) natively (no external app dependency). The protocol allows `http` or `https` per peer (announced in the discovery payload), and requires each participant to run an HTTP server for `/api/localsend/v2/register` (discovery callback) plus upload endpoints when receiving files.

Two decisions needed to be made before implementation:

1. **Transport security**: the official LocalSend app defaults to HTTPS with self-signed certificates and fingerprint pinning (SHA-256 of the cert, trust-on-first-use for unknown peers). Implementing this in C# means runtime certificate generation, pinning logic, and TOFU state management.
2. **HTTP server technology**: the app is an unpackaged WPF exe. `HttpListener` cannot bind a LAN (non-localhost) port without an elevated `netsh urlacl` reservation, which is unacceptable for a portable app.

## Decision

1. **Announce `protocol: "http"` for v1.** Plain HTTP over the LAN, protocol-compliant; official LocalSend clients interoperate with HTTP peers. HTTPS (runtime self-signed cert + fingerprint pinning) is a follow-up. As partial mitigation, the optional PIN (`401` on `prepare-upload`) is supported and disabled by default.
2. **Use Kestrel** via `<FrameworkReference Include="Microsoft.AspNetCore.App" />` — no new NuGet packages, production-grade HTTP/1.1 with streaming bodies for file upload/download, no elevation requirements.

## Consequences

- Files cross the LAN in cleartext; anyone sniffing the segment can read them. Acceptable for the home-LAN use case; users wanting a barrier can enable PIN (auth, not encryption). HTTPS follow-up tracked in #26.
- The app gains a dependency on the ASP.NET Core shared framework (framework-dependent deploy; no publish-size regression beyond what .NET 10 already requires).
- TCP port defaults to 53317; if occupied (e.g. official LocalSend app running), the service falls back to a random free port and announces the actual port — the protocol carries the port in the announcement payload, so peers connect correctly either way.
- Discovery coexistence: the UDP multicast socket binds 224.0.0.167:53317 with `ReuseAddress` so multiple LocalSend-compatible apps on one machine do not block each other.
