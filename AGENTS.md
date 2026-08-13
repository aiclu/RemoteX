# AGENTS.md

## What this is

RemoteX — a WPF desktop remote-session manager & launcher (RDP / SSH / VNC / Telnet / FTP / SFTP / Serial / RemoteApp). UI uses **Stylet** MVVM with a StyletIoC container. Root namespace is `_1RM` (assembly `RemoteX`). Default target framework is `net9.0-windows10.0.19041.0`; `ReleaseNet6` and `ReleaseNet48` configurations also exist. Nullable is enabled and `LangVersion=latest`.

## Commands

- Build (CLI): `.\Invoke-Build.ps1 Clean, Build -aReleaseType Debug` — or open `1Remote.sln` in Visual Studio 2022 and build the `Debug` configuration. Available Invoke-Build tasks: `Deps`, `Build`, `BuildInSandbox`, `Clean` (see `prm.build.ps1`).
- Test: `dotnet test Tests\Tests.csproj` (MSTest, targets `net6.0-windows10.0.17763.0`, references the `Ui` project).

## Where things live

| Path | Role |
|---|---|
| `Ui/` | Main WPF app. `Model/` protocol models, `View/` view+viewmodel pairs, `Service/` application services, `Utils/` helpers (incl. `mRemoteNG/` import, `PuTTY/`, `RdpFile/`), `Controls/` custom controls, `Resources/` themes / icons / language CSVs |
| `Ui/View/Host/` | Session hosts: `ProtocolHosts/` (RDP `AxMsRdpClient09Host`, `VncHost`, `FileTransmitHost`, `IntegrateHost`), `TabWindowView`, `FullScreenWindowView` |
| `Ui/Service/` | `ConfigurationService`, `DataSourceService`, `SessionControlService*` (session lifecycle), `LauncherService`, `ThemeService`, `LanguageService`, `TaskTrayService`, `KeywordMatchService` |
| `Ui/Service/DataSource/` | Data-source layer: SQLite (default), MySQL, PostgreSQL via a DAO abstraction (`DAO/Dapper` and `DAO/freesql` implementations) |
| `Shawn.Utils/` | Shared libraries (`Shawn.Utils`, `.Wpf`, `.WpfResources`) used by `Ui` |
| `VncSharpCore/` | VNC protocol (also published as the `1Remote.VncSharpCore` NuGet package) |
| `Dragablz/` | Forked tab-control library |
| `lib/` | Checked-in RDP COM interop assemblies `AxMSTSCLib.dll` / `MSTSCLib.dll` |
| `Installer/` | MSIX packaging for Microsoft Store |
| `Tests/` | MSTest suite; `TestInit.cs` mocks the IoC container by replacing `IoC.GetByType` |

## Rules for editing

- **IoC / services**: register long-lived services as singletons in `Ui/Bootstrapper.cs` (`ConfigureIoC`), then resolve with `IoC.Get<T>()`. `IoC.Translate("key")` resolves UI strings — add the key to the language CSV files under `Ui/Resources/Languages/`.
- **Property notifications**: use `SetAndNotifyIfChanged(ref _field, value)` with private `_camelCase` backing fields. WPF views bind to the PascalCase properties.
- **Protocol models** (`Ui/Model/Protocol/`): concrete protocols derive from `ProtocolBaseWithAddressPortUserPwd` (or a sibling base) and pass `(name, classVersion, displayName)` to the base constructor. When renaming an existing serialized field, keep old JSON names working with `[OtherName(Name = "OLD_NAME")]` and bump the `ClassVersion` string when the JSON shape changes. Editable fields must be surfaced in the matching editor form under `Ui/View/Editor/Forms/`.
- **New resources**: icons, images, and XAML pages must be registered explicitly in `Ui/Ui.csproj` (`<Resource Include>` / `<Page Update>`). SDK-style auto-include does not cover these.
- **New views**: create the XAML + code-behind + a `*ViewModel` (Stylet). Register singletons in `Bootstrapper` only if the window/VM is long-lived; otherwise bind transiently.
- **Session flow**: opening/closing a connection goes through `SessionControlService` (see `Ui/Service/SessionControlService*.cs`) and `TabWindowViewModel` — do not bypass it.

## Traps

- **Secrets**: non-`Debug` configurations run PreBuild/PostBuild targets that inject real secrets (App Center, Sentry, Salt) from `C:\RemoteX_Secret\` into `Ui/Assert.cs` and `Ui/AppVersion.cs`, then revert them. If that folder is missing the build fails — develop against the `Debug` configuration. Never commit real secrets.
- **`App.Close()`** intentionally spawns a delayed `Environment.Exit(1)` workaround — do not "fix" it.
- **GDI+ exceptions**: transient `ExternalException` failures from `WindowsFormsHost` painting are deliberately suppressed in `Bootstrapper.OnUnhandledException` (Windows 11 24H2 issue, ref #924). Preserve that filtering.
- **RDP hosting**: RDP uses the `AxMsRdpClient09` ActiveX control hosted in WPF through Windows Forms interop; `lib/` interop assemblies are checked in (regenerate with `scripts/MSTSCLib-Maker.ps1`).
- **Store builds**: `StoreDebug` / `StoreRelease` define `FOR_MICROSOFT_STORE_ONLY`, which activates UWP startup-activation code in `App.xaml.cs`.
- **Server ids**: persisted ids are ULIDs; `TMP_SESSION_`-prefixed ids are unsaved temporary sessions (`ProtocolBase.IsTmpSession`).
- **CI**: `.github/workflows/build-on-dev-push.yml` publishes net9.0 x64 (framework-dependent + self-contained) on push to master/main and tags. Commit messages containing `WIP` skip the build.
