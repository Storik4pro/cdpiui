# If the result is not suitable

### No suitable config was found

1. Check the spelling of the address and confirm that it is actually unavailable without Zapret2.
2. Use Balanced mode, then Exhaustive if necessary.
3. If you selected [One strategy for the whole list], try [Select for every site].
4. Make sure the appropriate HTTPS and IP options are selected.
5. Repeat the scan on a stable connection without changing your VPN, proxy, or DNS.

### Only a preliminary config was created

This is the best option found, but it did not pass the full threshold for every target. Open [Diagnostic information], find the failed sites, and test them manually. You can save this config for further work, but it should not be treated as fully verified.

If the report contains other successful strategies, try to [build a config manually](cdpiui://Help/Zapret2ManualPresetBuilding/1ManualPresetOverview/).

### The automated check succeeds, but the site does not work in a browser

Add the addresses used by the site for sign-in, images, video, APIs, or content delivery. Sometimes the main page is available while one of its supporting addresses remains blocked.

Also check IPv4 and IPv6. The browser may choose a different IP version from the one tested by the utility.

### A site was ignored because of DNS

The utility could not obtain a network address for the domain. Check the spelling and try to open it without Zapret2. If other applications cannot resolve it either, fix the connection or DNS settings before running another scan.

### A site list is missing from an old report

On the config review page, find the file with a warning and select [Replace]. Choose the current copy of the list. If the list has changed, running a new scan is more reliable.

### The scan was stopped or ended with an error

Open [Previous scans](cdpiui://Tools/AutoConfig/Zapret2/Reports). A partial report may still contain useful strategies. Save a JSON report when asking for support. Before sending it, remember that it may contain the tested addresses and paths to your files.

### For advanced users

An automated request tests one specific address, protocol, and IP version, but it does not reproduce the complete behavior of a browser. It does not test JavaScript or every additional page resource, and a browser may use different secure connection parameters.

The utility is intended primarily for HTTP and HTTPS sites. Discord voice connections and other arbitrary application UDP traffic cannot be tested this way.

Back to the beginning: [Selecting a config for Zapret2](cdpiui://Help/Zapret2PresetSelection/1AboutPresetSelection/).
