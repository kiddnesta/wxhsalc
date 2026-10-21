# Changelog

## v0.4.1 - 2026-10-22

### Fixed

- Changed **Test Latency** to use Mihomo's proxy-provider health-check API and test only HTTP provider nodes.
- Reloaded provider histories after testing so delay, timeout, and no-result states appear in the tray menus without blank status fields.
- Replaced the unconditional success message with an available/total provider-node summary.
- Re-register the tray icon before operation notifications so update, latency-test, and configuration-reload results remain visible after long-running sessions or notification-area refreshes.
- Added diagnostic logging whenever a tray notification is submitted or fails.
- Bundled the GeoIP database in release archives so configurations with `GEOIP` rules can start without a first-run download.

## v0.4.0 - 2026-10-02

### Added

- Added **Update Providers** (`Ctrl+U`) to the tray menu. It discovers configured HTTP proxy providers through the Mihomo API, updates each provider independently, and refreshes cached proxy data.
- Documented Mihomo's built-in Tailscale outbound, including Auth Key registration, persistent state, Tailnet routing rules, Remote Desktop testing, and safe removal of the Auth Key after enrollment.

### Changed

- Release builds continue to fetch the latest stable official `mihomo-windows-amd64-v3` core. Mihomo v1.19.25 or newer is required for the built-in Tailscale outbound; this release was verified with v1.19.32.
- Updated the README release link and corrected the configuration directory to `%APPDATA%\ClashXW\Config`.

### Tailscale protocol notes

- Mihomo documents Tailscale as an on-demand outbound proxy node. TCP works through the standard outbound dial path; Windows Remote Desktop (`TCP/3389`) was verified through the embedded Tailscale node.
- UDP is supported when `udp: true` is configured. Mihomo v1.19.25 release notes specifically record the Tailscale outbound moving to `ListenPacket` for UDP.
- The official documentation does not explicitly state that ICMP is unsupported. In v1.19.32 testing with the wxhsalc TUN path, ordinary ICMP echo traffic was logged as `DIRECT` and did not traverse the Tailscale proxy. Use an actual TCP/UDP service to verify the embedded node, or keep the desktop Tailscale client for system-level ping.
- The embedded Mihomo node and the desktop Tailscale client register as separate Tailnet devices with different Tailscale IP addresses.

### References

- [Mihomo Tailscale documentation](https://wiki.metacubex.one/en/config/proxies/tailscale/)
- [Mihomo Tailscale outbound PR #2786](https://github.com/MetaCubeX/mihomo/pull/2786)
- [Mihomo releases](https://github.com/MetaCubeX/mihomo/releases)
