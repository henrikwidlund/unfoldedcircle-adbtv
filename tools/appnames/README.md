# Android app names helper

`AppNames.java` contains the Android helper used to resolve package names to user-facing application labels through Android's `PackageManager`.

The compiled helper is stored in source control as:

```text
tools/appnames/appnames.dex
```

The .NET project embeds this DEX as the manifest resource `UnfoldedCircle.AdbTv.AppNames.dex`, so a normal local .NET build does not require the Android SDK, Java, or `d8`.

## Local build

For normal development, simply build the project:

```bash
dotnet build
```

The checked-in `appnames.dex` is used as-is.

## Modifying the Android helper

If you change `tools/appnames/AppNames.java`, you **must recompile the DEX and commit the updated `appnames.dex`**:

```bash
bash ./tools/appnames/generate-appnames-dex.sh
git add tools/appnames/AppNames.java tools/appnames/appnames.dex
git commit
```

Regenerating the DEX requires `javac` and Android Build Tools (`d8`). If `d8` is not on `PATH`, set either `ANDROID_SDK_ROOT`, `ANDROID_HOME`, or `APPNAMES_D8=/path/to/d8`.

## Runtime

`AppNamesHelper.cs` reads the embedded DEX, calculates its SHA-256-based remote filename, and Base64-encodes it only when preparing the ADB upload. No Base64 representation of the DEX is maintained in the C# source.

## GitHub Actions

CI regenerates the DEX before the .NET build. Release builds also regenerate it once on Ubuntu and pass the generated DEX to all Linux, macOS, and Windows builds, so released binaries are always built from the current `AppNames.java` source.
