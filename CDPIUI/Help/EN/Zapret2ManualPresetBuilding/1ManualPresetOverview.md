# Building a config manually

The manual builder lets you choose strategies from a scan report yourself. Use it when the automatically suggested config does not work in a browser, the scan produced only a partial result, or you want to replace one strategy.

The builder does not scan the complete catalog again. It uses the results already collected during the scan, but it can retest an individual strategy.

### When to use the manual builder

- The report contains successful strategies, but the complete automatic config does not work.
- Different sites in one list need different options.
- The automatic choice is unstable or uses a strategy you do not want.
- You want to keep only options that you have tested manually.

If no strategy passed for the required site, manual assembly cannot make one work. [Run another scan](cdpiui://Tools/AutoConfig/Zapret2/) with a different mode or site list instead.

### Open the builder

1. Open the result of the completed scan or select it under [Previous scans](cdpiui://Tools/AutoConfig/Zapret2/Reports).
2. In [How do you want to continue?], select [Manual builder].
3. If that dialog is already closed, open [Preset builder] at the top of the result window.

The builder initially contains the automatically selected strategies. You can use them as a starting point, change them, or select [Remove all] to start with an empty config.

Every strategy you add should be tested on the site and connection type where you plan to use it.

Next: [choose strategies](cdpiui://Help/Zapret2ManualPresetBuilding/2ChooseManualStrategies/).
