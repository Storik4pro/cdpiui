# Choose and prepare files

1. [Open the import utility](cdpiui://Tools/ImportConfig) and select **Next**.
2. Select one or more preset files in the file picker.
3. Wait for analysis to finish. When several files are selected, the wizard processes them one at a time and shows the current file name.

### Which format should I select?

- Select `.bat` or `.cmd` when the preset is distributed as a ready-to-run script.
- Select `.txt` when the file contains component launch arguments.
- Select `.json` only for an exported CDPIUI preset or a compatible preset in the older format.

If the main file has nearby `lists`, `bin`, `fake`, or `lua` folders, keep the package structure unchanged until import is complete. This helps the wizard find its resources.

### What happens after analysis?

- If all required dependencies are found, the result page opens.
- If some files are missing, the **Missing files** page appears first.
- If a file cannot be recognized, its result card remains available with the **Failed** status. Select the status to see the reason.

### For advanced users

The wizard is not a complete Windows command-line interpreter. It looks for a recognizable launch of an installed component and extracts its arguments. A file with several possible launches, complex conditions, loops, or dynamically assembled commands may need to be simplified. A regular BAT or CMD file should contain one unambiguous component launch.

Next: [resolve missing files](cdpiui://Help/ConfigImport/3ResolveMissingFiles/).
