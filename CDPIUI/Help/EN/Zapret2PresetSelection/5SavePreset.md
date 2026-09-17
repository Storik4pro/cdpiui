# Testing and saving the config

The review page shows the completed config text and its related files. The text is read-only by default to prevent accidental changes.

### Test the config before saving

1. Make sure [Used files] does not contain missing TXT lists or libraries.
2. Start the config temporarily.
3. Open the required sites in your normal browser or test the required application.
4. Test more than the main page: check sign-in, images, video, and other important features.
5. Stop the temporary process after testing.

If a file has a warning, select [Replace] and choose the current file. This can happen when you open a report from another computer or after moving a custom list.

### Save to CDPIUI

Select [Save as new config], enter a clear name, and confirm. CDPIUI adds the config to [Configs] and copies the required custom lists and external files into application storage. Your original files are not changed.

If the save button is unavailable, check the name, the Zapret2 installation, and warnings about missing files.

### Reports and history

The scan result automatically appears under [Previous scans](cdpiui://Tools/AutoConfig/Zapret2/Reports). From there, you can open a report, show its file in the folder, or remove it from history.

### For advanced users

- A **JSON report** preserves complete structured results and is best for reopening the scan, manual assembly, or diagnostics.
- A **text report** is easier to read but contains less information.
- **Allow editing** enables expert mode. Use it only if you understand Zapret2 parameters. If you return to the builder, direct text changes are removed because they cannot be converted back into the selected sites and strategies without losing information.

If the saved config does not work as expected, see [If the result is not suitable](cdpiui://Help/Zapret2PresetSelection/6TroubleshootingPresetSelection/).
