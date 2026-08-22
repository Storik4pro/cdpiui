# Import presets from files

This utility helps bring presets from other applications or saved files into CDPIUI. It recognizes launch arguments, finds related lists and libraries, and lets you test and save the result.

[Open the import utility](cdpiui://Tools/ImportConfig)

### Which files are supported?

- `.bat` and `.cmd` component launch scripts;
- `.txt` files containing launch arguments;
- `.json` presets in a supported CDPIUI format.

You can select several files at once. The wizard creates a separate result card for each file.

An ordinary JSON document, a report from another utility, or an unknown application's settings file is not a CDPIUI preset and may not be imported.

### What should I prepare?

- Install the component for which the preset was created. Components that are not installed cannot be selected or tested.
- Keep the BAT, CMD, or TXT file together with its list, BIN, and LUA folders. The wizard will try to find and copy these dependencies.
- Keep a backup of the original package when possible.

### Is the analysis safe?

During analysis, CDPIUI only reads the selected files. It does not run the source BAT or CMD script, execute its commands, or modify or delete the original files.

The test button on the result page is different: it starts the selected installed component with the imported arguments. Use it only with files from a source you trust.

Next: [choose and prepare files](cdpiui://Help/ConfigImport/2ChooseImportFiles/).
