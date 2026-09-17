# Errors and limitations

If a card has the **Warnings** or **Failed** status, select the status. The details show a problem code and, when available, the source line number.

### Common situations

**The JSON file is not supported**

The utility accepts CDPIUI JSON presets in a supported format. An arbitrary JSON document, a Zapret2 scan report, or settings from another application cannot be imported merely because it uses the `.json` extension. Find an exported preset or use a BAT, CMD, or TXT file containing launch arguments.

**The component could not be detected or selected**

Install the required component from the CDPIUI Store first. A component is available to the wizard only when its folder and executable exist on this computer.

**The preset is not suitable for the selected component**

Select the component named by the preset author. Similar purpose does not mean that two components support the same arguments.

**A referenced file is missing**

Return to the original preset archive or folder and locate the required list, BIN, or LUA file. Select it manually. Create an empty file only for a genuinely optional resource.

**No component launch was found, or several launches were found**

The wizard could not extract one unambiguous set of arguments. Try another file from the package, remove extra launch alternatives from a copy of the BAT or CMD file, or save the required arguments in a separate TXT file.

**The test exits immediately, or the saved preset does not work**

Check the selected component, warnings, and missing-file decisions. Make sure you imported the working launch variant. Some presets contain additional values that must be configured in the component settings.

### For advanced users

Static analysis can process only known structures. Complex `if`, `for`, and `goto` logic, computed variables, nonstandard include files, wrapper launches, and arbitrary resource generation may be recognized only partially. Source BAT and CMD commands are not executed during analysis, so the result of such a script may require manual correction.

Back to the beginning: [Import presets from files](cdpiui://Help/ConfigImport/1AboutConfigImport/).

[Start a new import](cdpiui://Tools/ImportConfig)
