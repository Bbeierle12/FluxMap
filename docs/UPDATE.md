# Update Strategy

## Core service
- Release builds are produced with `core/scripts/build-release.ps1`.
- Packages are zipped via `core/scripts/package-zip.ps1`.
- The service reads `appsettings.Local.json` if present; upgrades should preserve that file.

## UI
- The UI is static content. Packaging copies `ui-web` into the core output.
- Updates replace the `ui-web` folder in the release bundle.

## Agent
- The Kali agent reads a local `updateCheckFile` with a JSON `{ "version": "x.y.z" }`.
- The agent does not auto-download; it exposes update status in `/stats`.

## Versioning
- Bump the agent version in `agent-kali/agent.py`.
- Track core version in release notes (optional file `docs/RELEASES.md`).
