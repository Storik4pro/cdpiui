# Review, test, and save

On the **Import complete** page, every selected file has its own card. Review the card before saving it.

### Name and component

- Change the name if the source file name is not clear enough. The preset appears under this name in the component settings.
- Make sure the correct component is selected. Only installed components appear in the list.
- If a yellow compatibility warning appears, select the component for which the preset was created. Ignore the warning only when you know the arguments are compatible.

### Status and details

- **Ready** means the wizard found no issue that blocks saving.
- **Warnings** means saving is available, but the result requires attention.
- **Failed** means an error blocks testing and saving.

Select the status to see details and the problem code. A warning does not necessarily mean the preset is broken, while **Ready** does not replace a real test of the required sites.

### Test the preset

The button with the triangle starts the selected component with the imported arguments. Check the required sites, then select the button again to stop the test.

An already running instance of that component is stopped before the test. The previous preset is not restored automatically afterward; start it again from the regular component settings if needed.

The test starts the real executable of the installed component and is not a sandbox.

### Save the preset

- The disk button saves one card.
- **Save all** saves every ready card in sequence.
- Successfully saved cards disappear from the list. Cards with errors remain so that you can open their details.

After saving, select the new preset in the corresponding component settings. If it contains additional switches or values, configure them before normal use.

### For advanced users

When Zapret2 is selected for a Zapret Legacy preset, the wizard can convert the final `winws` arguments to `winws2`. Reverse conversion from Zapret2 to Zapret Legacy is not supported. No specialized conversion is performed for other mismatched components.

If something went wrong: [errors and limitations](cdpiui://Help/ConfigImport/5ConfigImportTroubleshooting/).
