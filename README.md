# NetWatch

Local-first home-network discovery and monitoring.

## Structure
- `core/`: Windows service (ASP.NET Core minimal API)
- `ui-web/`: local web UI (can be wrapped by Electron later)
- `agent-kali/`: optional Kali defensive agent (Python)
- `shared/`: shared schemas
- `docs/`: architecture and notes

## Kali agent
Configure `agent-kali/config.json` with `apiBase` and optional `token`.
If you set `Agent:Token` in `core/src/NetWatch.CoreService/appsettings.json`,
the agent must send `X-NetWatch-Token` to post observations.
The agent exposes a local status server at `http://127.0.0.1:8787` by default:
`/health` and `/stats` (last post time, error count).
Set `Agent:HmacSecret` to require HMAC signatures. The agent then sends
`X-NetWatch-Timestamp` and `X-NetWatch-Signature` headers.
To use one-time registration, set `Agent:RegistrationEnabled` and
`Agent:RegistrationCode` on the core, then set `registrationCode` in the agent
config; the agent will request a token and save it locally.
Agent batching can be tuned with `queueMax`, `batchSize`, and
`batchIntervalSeconds` in `agent-kali/config.json`.

## TP-Link, Netgear, Orbi, Omada, and Asus connectors
TP-Link, Netgear, Orbi, Omada, and Asus adapters are generic DHCP lease fetchers that expect a
HTTP endpoint returning leases in `json`, `csv`, or `keyvalue` formats. Configure
URL/auth and fields in the UI under Connectors.

## Ops
- Copy `core/src/NetWatch.CoreService/appsettings.Local.example.json` to `appsettings.Local.json`
  to override log file path and UI static path.
- The core service can run as a Windows Service (UseWindowsService enabled).
- Install/uninstall scripts live in `core/scripts`.

## Packaging
- `core/scripts/build-release.ps1` publishes the core and bundles `ui-web`.
- `core/scripts/package-zip.ps1` zips the release output.
- See `docs/UPDATE.md` for the update strategy.
- See `docs/INSTALLER.md` for MSI/MSIX notes.

## Desktop GUI
- WinUI 3 shell lives in `desktop` and hosts the local UI at `http://127.0.0.1:5000`.
- Run it with: `dotnet run --project C:\Users\Bbeie\NetWatch\desktop`.

## MSI Installer
1) Build core: `core/scripts/build-release.ps1`
2) Build desktop: `desktop/scripts/build-release.ps1`
3) Build MSI: `installer/build-msi.ps1`
