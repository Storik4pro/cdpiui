# Results and config assembly

When the scan finishes, select [View result]. You can open an older report under [Previous scans](cdpiui://Tools/AutoConfig/Zapret2/Reports).

The result window contains [Selection result], [Preset builder], and [Diagnostic information].

### Selection result

This page shows how each strategy worked for each site and connection type.

- **Result**, such as `5/5 (100%)`, means that all five attempts succeeded.
- **Response**, such as `45 ms`, shows the response time. Consider the success rate first and speed second.
- **Without Zapret2** shows the normal connection result. If a site is already consistently available without bypass settings, it does not need a separate strategy.
- A warning icon means that the row passed, but the strategy was not confirmed for every related connection type for that site.

Search, filtering, and sorting help hide irrelevant rows. A convenient starting point is to show successful results and use [Best results first].

### Preset builder

CDPIUI adds the automatically selected strategies to the builder in advance. If the result is successful and there are no warnings, you can usually leave this list unchanged and select [Proceed to testing and saving the config].

Order matters: a more specific option for one site must take priority over a broader option. The warnings panel reports overlaps, partial coverage, and other possible problems.

### For advanced users

You can use the selected result row to:

- repeat the automated request test;
- temporarily start the strategy for a manual browser test;
- add it for the current site or a related site list;
- copy either the displayed actions or the complete ready-to-use test strategy.

Manual assembly is useful when the automatically suggested option fails a real-world test. Do not choose a strategy because of a single successful attempt: compare repeatability for every required connection type.

For detailed steps, see [Build a config manually](cdpiui://Help/Zapret2ManualPresetBuilding/1ManualPresetOverview/).

### Diagnostic information

This page contains scan issues and the complete list of network attempts. It is mainly useful for finding the cause of a failure or asking for support. Short issue codes are technical identifiers; use the explanation shown next to each code.

Next: [test and save the config](cdpiui://Help/Zapret2PresetSelection/5SavePreset/).
