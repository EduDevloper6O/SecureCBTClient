# Secure CBT Client

WPF desktop client for a candidate computer-based examination workflow. The active solution is `Elecctron CBT.slnx`, and the application targets `.NET 10` on Windows.

## Automatic updates

The application uses `Autoupdater.NET.Official`. At startup it checks this stable GitHub Releases URL:

`https://github.com/EduDevloper6O/SecureCBTClient/releases/latest/download/update.xml`

The update manifest and installer are created automatically by `.github/workflows/release.yml` whenever a version tag is pushed. Updates are distributed through GitHub Releases, not through the source-code download page.

## One-time GitHub setup

1. Make sure the repository is named `SecureCBTClient` under the `EduDevloper6O` account, or change `UpdateManifestUrl` in `MainWindow.xaml.cs` and the URLs in the workflow.
2. Push this workflow and the `installer/SecureCBT.iss` script to the repository.
3. Ensure GitHub Actions is enabled for the repository.
4. On GitHub, open **Settings > Actions > General** and allow workflows to create and update releases if the repository policy requires that permission.
5. Install the current application once using the installer produced by a release. Do not copy only the build folder to client computers; the installer is what AutoUpdater.NET launches for an update.

## Publishing a new version

Use a four-part version tag. The tag must be higher than the version installed on client computers.

```powershell
git add .
git commit -m "Prepare release 1.0.1.0"
git push origin main
git tag v1.0.1.0
git push origin v1.0.1.0
```

GitHub Actions then:

1. Publishes a self-contained `win-x64` application.
2. Installs Inno Setup on the build runner.
3. Builds `SecureCBT_Installer.exe` using `installer/SecureCBT.iss`.
4. Generates `update.xml` with the release version and installer URL.
5. Creates a GitHub Release and uploads the installer and `update.xml`.

When an installed client starts, AutoUpdater.NET compares its assembly version with the `<version>` in the latest release manifest. If the release is newer, the installer is downloaded and started with `/SILENT`.

## Important release rules

- Always use tags in the form `v1.2.3.4`.
- Never reuse a tag or version for a different build.
- Keep `SecureCBT_Installer.exe` as the installer asset name unless the workflow and update manifest are changed together.
- The installer uses a stable Inno Setup application ID so updates target the existing installation directory.
- Build and test the application locally before pushing a release tag.

## Candidate flow

1. The client prepares the examination session and opens candidate access.
2. The candidate signs in with a roll number, password or PIN, and exam code.
3. The instructions screen shows the candidate profile and scheduled start countdown.
4. During the examination, candidates move through sections and questions, select or clear answers, mark questions for review, and submit.
5. After submission, the client loads the response summary and score.

## Local development

Install Visual Studio with the **.NET desktop development** workload, or install the .NET 10 SDK and Windows Desktop Runtime. Then restore and build:

```powershell
dotnet restore "Elecctron CBT.csproj"
dotnet build "Elecctron CBT.csproj"
```