# Selecting a config for Zapret2

This utility helps create a Zapret2 config for the sites you need. A config is a saved set of settings that the component can use later.

The utility automatically:

1. Checks whether the selected sites work without Zapret2.
2. Tests different strategies — alternative Zapret2 settings.
3. Chooses the most reliable options and combines them into one config.
4. Tests the complete config and, when possible, replaces conflicting options.
5. Saves a report so you can return to the result later.

[Start a new scan](cdpiui://Tools/AutoConfig/Zapret2/)

[Show previous scans](cdpiui://Tools/AutoConfig/Zapret2/Reports)

### When this utility is useful

- The sites you need do not work with your installed configs.
- You want one config for several sites or site lists.
- You want to test different settings automatically instead of trying each one yourself.

The Zapret2 component must be installed in CDPIUI before you can start a scan.

### Important details

- Only the addresses you add are tested. If a service uses separate addresses for images, video, sign-in, or its API, you may need to add those addresses as well.
- A successful automated check does not guarantee that the entire page or application will work. Always test the config in your normal browser or application afterward.
- CDPIUI temporarily stops active network components during the scan. Internet access on this computer may be unavailable until the scan finishes or is canceled.
- A stopped or partially successful scan will usually still provide a report with the results collected so far.

If the automatically suggested config does not work for you, follow [Build a config manually](cdpiui://Help/Zapret2ManualPresetBuilding/1ManualPresetOverview/).

Next: [prepare for a scan](cdpiui://Help/Zapret2PresetSelection/2ChooseSitesAndMode/).
