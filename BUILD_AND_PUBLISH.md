# Build and Publish VRates

## Local build

Install the .NET 6 SDK, then run:

```bash
dotnet restore src/VRates/VRates.csproj --configfile nuget.config
dotnet build src/VRates/VRates.csproj -c Release --no-restore
```

The DLL is produced at:

```text
src/VRates/bin/Release/net6.0/VRates.dll
```

## GitHub Actions

The workflow at:

```text
.github/workflows/build.yml
```

builds the DLL, creates a Thunderstore-ready ZIP, validates its contents, and uploads two artifacts:

```text
VRates-dll
VRates-Thunderstore-v1.0.0
```

## Thunderstore package layout

```text
manifest.json
README.md
CHANGELOG.md
SOURCE_REVIEW.md
icon.png

BepInEx/plugins/VRates/VRates.dll

Source/Plugin.cs
Source/VRates.csproj
Source/BUILD_COMMIT.txt
```

The source files in the package are byte-for-byte copies of the files used by CI.

## Release

1. Push the desired source to the public GitHub repository.
2. Confirm GitHub Actions succeeds.
3. Download the `VRates-Thunderstore-v1.0.0` artifact.
4. Test the packaged DLL on a server/host.
5. Upload that ZIP to Thunderstore.

Do not rebuild the DLL manually after CI if the package is intended for release; use the CI artifact so `BUILD_COMMIT.txt` accurately identifies the build source.
