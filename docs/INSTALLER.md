# Installer Notes

## MSI (WiX)
- Build release with `core/scripts/build-release.ps1`.
- Use WiX Toolset to create an MSI that installs the core service binaries and UI.
- Configure the service to run `NetWatch.CoreService.exe` and set recovery options.

## MSIX (alternative)
- Package the `dist/core` folder into an MSIX if you prefer app‑installer updates.
- Ensure `appsettings.Local.json` is preserved on upgrade.
