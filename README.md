# Unfolded Circle ADB TV Integration Driver

[![Release](https://img.shields.io/github/actions/workflow/status/henrikwidlund/unfoldedcircle-adbtv/github-release.yml?label=Release&logo=github)](https://github.com/henrikwidlund/unfoldedcircle-adbtv/actions/workflows/github-release.yml)
[![CI](https://img.shields.io/github/actions/workflow/status/henrikwidlund/unfoldedcircle-adbtv/ci.yml?label=CI&logo=github)](https://github.com/henrikwidlund/unfoldedcircle-adbtv/actions/workflows/ci.yml)
![Sonar Quality Gate](https://img.shields.io/sonar/quality_gate/henrikwidlund_unfoldedcircle-firetv?server=https%3A%2F%2Fsonarcloud.io&label=Sonar%20Quality%20Gate&logo=sonarqube)
[![Qodana](https://img.shields.io/github/actions/workflow/status/henrikwidlund/unfoldedcircle-adbtv/qodana_code_quality.yml?branch=main&label=Qodana&logo=github)](https://github.com/henrikwidlund/unfoldedcircle-adbtv/actions/workflows/qodana_code_quality.yml)
[![Docker](https://img.shields.io/github/actions/workflow/status/henrikwidlund/unfoldedcircle-adbtv/docker.yml?label=Docker&logo=docker)](https://github.com/henrikwidlund/unfoldedcircle-adbtv/actions/workflows/docker.yml)

This repository contains the server code for hosting an integration driver that uses ADB for communication for the Unfolded Circle Remotes.
It exposes a Remote Entity that can be used to control TVs or any other device based on Android with support for ADB.

Tested on Panasonic Z95B

### Limitations

- The integration relies on ADB (Android Debug Bridge) to communicate with the device. This is useful for devices that don't expose other APIs.
The downside is that this protocol is very slow, as such, you should use Bluetooth for as many commands as possible.
- Reauthorization of the ADB connection is required when reinstalling/updating the integration when it is hosted
on the remote. This is because the public and private keys are removed when the integration is uninstalled.
- The power states are dumb, it only tracks on or off when triggered via the integration.
This is a limitation of the ADB protocol, where it is not possible to query the power state without turning on the device.

### Prerequisites
- IP and MAC address of the device you want to control.
- When installing on the remote, the remote must be on firmware version 2.6.3 or later or the driver will crash
because it can't start adb.

### Running

- The published binary is self-contained and doesn't require any additional software.
It's compiled for Linux ARM64 and is meant to be running on the remote.
- Use the [Docker Image](https://hub.docker.com/r/henrikwidlund/unfoldedcircle-adbtv) in the [Core Simulator](https://github.com/unfoldedcircle/core-simulator)
- Other Operating Systems - Linux, macOS, Windows - are supported. Requires that you have ADB installed.

### Network

| Service      | Port   | Protocol   | Location              |
|--------------|--------|------------|-----------------------|
| Server       | 9001*  | HTTP (TCP) | Remote/other computer |
| ADB          | 5555** | TCP        | Device to control     |
| Wake on Lan  | 9      | UDP        | Device to control     |

\* Server port can be adjusted by specifying the desired port with the `UC_INTEGRATION_HTTP_PORT` environment variable.
\** ADB port can be adjusted during configuration if your device uses a different port.

### Additional commands

You can send any `input keyevent` command with the remote entity if it's added to an activity.
A list of commands can be found in the official docs at [Android KeyEvent](https://developer.android.com/reference/android/view/KeyEvent)
and [here](https://gist.github.com/arjunv/2bbcca9a1a1c127749f8dcb6d36fb0bc). Make sure to only use the digits in the commands.

#### Advanced commands
You can send any `adb shell` command with the integration. Use the following prefixes to send commands:
- `RAW:YOUR_COMMAND` - Sends the command as is, without any modifications.
- `APP:YOUR_COMMAND` - Starts an application by sending `shell monkey --pct-syskeys 0 -p {YOUR_COMMAND} 1`.
- `ACT:YOUR_COMMAND` - Starts an activity by sending `shell am start -n {YOUR_COMMAND}`.
- `INP:YOUR_COMMAND` - Switches input by sending `shell am start -a android.intent.action.VIEW -d content://android.media.tv/passthrough/com.mediatek.tvinput%2F.hdmi.HDMIInputService%2FHW{YOUR_COMMAND} -n org.droidtv.playtv/.PlayTvActivity -f 0x10000000`.
Make sure to not include the `adb shell` part of the command, device IP, ports and similar, as it is already included by the integration.
Also make sure that you do not have any spaces between the prefix and the command.

### Development

- [dotnet 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).
- or [Docker](https://www.docker.com/get-started).

## Installing on the remote

1. Download `unfoldedcircle-adbtv.tar.gz` from the release page
2. Open the remote's Web Configurator
3. Click on `Integrations`
4. Click on `Add new` and then `Install custom` 
5. Choose the file in step 1 (`unfoldedcircle-adbtv.tar.gz`)
6. Make sure that your device is turned on
7. Click on the newly installed integration and follow the on-screen instructions

## Configuration

The application can be configured using the `appsettings.json` file or environment variables.
Additionally, the application saves configured entities to the `configured_entities.json` file, which will be saved to the directory specified by the `UC_CONFIG_HOME` environment variable.

## Logging

By default, the application logs to stdout. 
You can customize the log levels by either modifying the `appsettings.json` file or by setting environment variables.

### Log levels
- `Trace`
- `Debug`
- `Information`
- `Warning`
- `Error`

`Trace` log level will log the contents of all the incoming and outgoing requests and responses. 

### `appsettings.json`

```json
{
    "Logging": {
        "LogLevel": {
          "UnfoldedCircle.Server": "Information",
          "UnfoldedCircle.AdbTv": "Information",
          "Makaretu.Dns": "Warning"
        }
    }
}
```

### Environment variables

Same adjustments to log levels can be made by setting environment variables.
- `Logging__LogLevel__UnfoldedCircle.Server` = `Information`
- `Logging__LogLevel__UnfoldedCircle.AdbTv` = `Information`
- `Logging__LogLevel__Makaretu.Dns` = `Warning`

## Building from source code

### Building for the remote

Execute `publish.sh` script to build the application for the remote. This will produce a `tar.gz` file in the root of the repository.

### Building for Docker

Execute the following from the root of the repository:

```sh
docker build -f src/UnfoldedCircle.AdbTv/Dockerfile -t adbtv .
```

### dotnet CLI

```sh
dotnet publish ./src/UnfoldedCircle.AdbTv/UnfoldedCircle.AdbTv.csproj -c Release --self-contained -o ./publish
```

This will produce a self-contained binary in the `publish` directory in the root of the repository.

## Licenses / Copyright

- [License](LICENSE)
- [richardschneider/net-dns](https://github.com/richardschneider/net-dns/blob/master/LICENSE)
- [richardschneider/net-mdns](https://github.com/richardschneider/net-mdns/blob/master/LICENSE)
- [jdomnitz/net-dns](https://github.com/jdomnitz/net-dns/blob/master/LICENSE)
- [jdomnitz/net-mdns](https://github.com/jdomnitz/net-mdns/blob/master/LICENSE)
