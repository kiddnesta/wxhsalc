# wxhsalc

<img width="435" height="255" alt="image" src="https://github.com/user-attachments/assets/fc35cdfb-dbcc-4797-b95b-171932a7ed7b" />  
  
This is a fork mainly with human-machine interface improvements & personal preferences.

## Changes

- Use Windows default program to edit config.
- Remembers window size and placements.
  - Multi-monitor set-up should work but not tested.
- Update all HTTP proxy providers independently from the tray menu with **Update Providers** (`Ctrl+U`).
- Test only HTTP proxy-provider nodes with **Test Latency**; results are shown beside each node as a delay, timeout, or no-result state.
- Supports Mihomo's built-in Tailscale outbound when used with Mihomo v1.19.25 or newer.
- Use multi-column context menu ( in another branch ).
  - Dark mode for this kind of context menu is not working.

## Plan

- [ ] Switch to external strings for future translations.

## Why

1. I believe proxy experience should be as transparent as possible, a clunky GUI really doesn't feel like so.
2. Most GUI clients tend to mess up my config file, they don't fully respect my clash config.
3. Most GUI clients install a service to avoid UAC prompts, convenient but I really don't like having another third-party service running background.
4. Most GUI clients are ugly (sorry). They don't respect platform native design language.
5. The old [ClashXW](https://github.com/ysc3839/ClashXW) is great, big shoutout to the original author, but it doesn't have a TUN toggle button, nor `hidden` property support. (If you don't need these features, it might be better to just use the original ClashXW)

## This is not for you if

1. You don't know what Clash/Mihomo is.
2. You don't know how to use Clash/Mihomo **core**.
3. You want a fancy GUI client.
4. You want minimal performance overhead, I am a shit coder.

## How to use

1. Download the latest release from the [Releases](https://github.com/kiddnesta/wxhsalc/releases) page.
2. Extract the downloaded archive to a folder of your choice.
3. Run the `ClashXW.exe` file.
4. Edit the configuration file under `%APPDATA%\ClashXW\Config`. You can have multiple config files there.
5. Reload the application to apply the changes.

## Tailscale outbound

wxhsalc can use Mihomo's built-in Tailscale outbound. Mihomo v1.19.25 or newer is required; release builds fetch the latest stable `mihomo-windows-amd64-v3` automatically.

Add a Tailscale proxy to the existing `proxies` section. Generate a one-off, pre-authorized key from the [Tailscale Keys page](https://login.tailscale.com/admin/settings/keys), fill it locally, and never commit the key:

```yaml
proxies:
  - name: Tailscale
    type: tailscale
    hostname: wxhsalc
    auth-key: 'tskey-auth-REPLACE-ME'
    state-dir: ./tailscale
    udp: true
    accept-routes: true
```

Route Tailnet addresses and MagicDNS names through the proxy. Put these rules before the final `MATCH` rule:

```yaml
rules:
  - DOMAIN-SUFFIX,ts.net,Tailscale
  - IP-CIDR,100.64.0.0/10,Tailscale,no-resolve
```

Reload the configuration, then make a TCP or UDP connection to another Tailnet device. Tailscale proxy nodes start lazily, so the first connection can take longer or time out while the node signs in. After the device appears in the Tailscale admin console and `state-dir` has been created, remove `auth-key` from the YAML; the persisted state is sufficient for later starts.

TCP services such as Remote Desktop are supported. For example:

```powershell
mstsc /v:100.x.y.z
curl.exe -v --proxy http://127.0.0.1:7890 telnet://100.x.y.z:3389
```

UDP requires `udp: true`. Ordinary ICMP ping is not a valid test for this userspace proxy outbound: with the current Mihomo TUN integration it is handled as `DIRECT`, rather than through the Tailscale proxy. Keep the desktop Tailscale client running if system-level ICMP ping is required.

The desktop Tailscale client and Mihomo's embedded Tailscale node are separate Tailnet devices and receive different `100.x.y.z` addresses.

## Bonus

- If you want to use TUN mode, you need to run the program with Administrator privileges.
- You don't need to wait for this application to update if you want to use a newer version of clash. You can simply replace the `clash.exe` file in the installation directory with the newer version. Or open the dashboard, go to Settings and click **Upgrade Clash Core**.
- See the official [Mihomo Tailscale documentation](https://wiki.metacubex.one/en/config/proxies/tailscale/) for advanced options such as Headscale, subnet routes, and exit nodes.

## Development

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Building

To build the project for debugging:

```sh
dotnet build
```

### Publishing

To create a release build:

```sh
dotnet publish --configuration Release
```

The self-contained application will be located in the `bin/Release/net8.0-windows/publish/` directory.
