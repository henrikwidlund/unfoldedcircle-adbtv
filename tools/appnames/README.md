# Android app names helper

`AppNames.java` contains the Android helper used to resolve package names to user-facing application labels through Android's `PackageManager`.

The compiled helper is generated at build time as `tools/appnames/appnames.dex`. The generated DEX is not stored in source control. The .NET project embeds it as the manifest resource `UnfoldedCircle.AdbTv.AppNames.dex` when the generated file exists.

## Local build

To use application-label resolution in a local build, generate the helper first:

```bash
bash ./tools/appnames/generate-appnames-dex.sh
dotnet build
```

Generation requires `javac` and Android Build Tools (`d8`). If `d8` is not on `PATH`, set `ANDROID_SDK_ROOT`, `ANDROID_HOME`, or `APPNAMES_D8`.

## Modifying the Android helper

After changing `tools/appnames/AppNames.java`, regenerate the DEX locally to validate the source. The generated DEX remains an untracked build artifact.

## Runtime

`AppNamesHelper.cs` reads the embedded DEX, calculates its SHA-256-based remote filename, and Base64-encodes it only when preparing the ADB upload.

## GitHub Actions

CI, release, and Docker workflows generate the DEX from `AppNames.java` before packaging. This keeps packaged binaries reproducible from reviewed source.
