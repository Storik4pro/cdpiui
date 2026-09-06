# ConfigShare

`.cdpiconfig` is a ZIP archive containing a single preset and the resources it uses.
`ConfigShareService` provides export, archive validation, extraction and installation;
`ConfigImportService` and `ConfigImportInstaller` delegate this format to it.

## Format v1

`manifest.json` contains `Version`, `Name`, `Developer`, `Config` (a `ConfigItem`),
and `Resources`. Each resource has `Path`, `Length`, `Sha256` and `RewriteReferences`.
Resources live under `resources/`; portable references use `share://resources/...`.
The file list comes exclusively from `ConfigItem.UsedFiles`. Paths are resolved with
`ConfigItem.ResolveFilePath`, using the component directory and preset pack directory.
ConfigShare only packages that list and rewrites the corresponding preset references.
Files, including component libraries, are copied unchanged; ConfigShare does not parse
Lua or enumerate additional dependencies. Output files excluded by `UsedFiles` are not
packaged. Missing files reported by `UsedFiles` produce an export error.

The reader checks format version, resource hashes, declared sizes, duplicate names,
entry count and safe relative paths before installation. Limits: 4096 archive entries,
512 MiB uncompressed total and 4 MiB manifest. Archives cannot contain filesystem links.

## Lifecycle

`ExportAsync` and `ReadAsync` return an owning `ConfigSharePackage`. Dispose it after
copying the exported file or completing/canceling import. Before Windows sharing,
`RetainForSystemShare` marks its directory so disposing the package keeps the file.
Opening the next export dialog calls `CleanupPreviousSystemShares`, which removes
these marked directories even after an application restart. Other in-progress imports
and exports are not touched. Temporary directories are allocated under `TempFiles` with
`FileSystemService.GetNewTempFileName` and a GUID to prevent name collisions.

`InstallAsync` creates resources in a unique `Shared/<GUID>` directory. It can install
into `LocalUserData` or an existing GUID kit, or register a new GUID kit. Store IDs
are never valid destinations. Failure removes only the files created by that import.
Reimporting creates a new preset and never overwrites an existing preset or resource.

Dependencies not reported by `UsedFiles` must be explicitly attached as preset resources.

## UI and validation

The common component `ConfigSelector` opens the export dialog. File activation opens
`ConfigShareImportDialog`; the ConfigImport wizard keeps its usual review/test/save UI.
Export and message layouts are defined in `Controls/Dialogs/ConfigShare/*.xaml`;
their code-behind handles actions and state changes.
Both entry points offer the existing Store component installer when the required
component is missing. Zapret Legacy is considered available when Zapret2 is installed;
the wizard uses the existing Legacy-to-Zapret2 conversion in that case.
Windows sharing uses [per-window desktop interop](https://learn.microsoft.com/windows/apps/develop/ui/display-ui-objects).

Run `dotnet run --project CDPIUI.AddOns.ConfigShare.Regression` for isolated round-trip,
dependency, destination, malformed archive and cleanup checks. The regression program
uses its own data directory; it does not modify the installed application's presets.
