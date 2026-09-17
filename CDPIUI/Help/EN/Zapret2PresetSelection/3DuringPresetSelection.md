# Starting and monitoring the scan

On [Review settings], select [Start selection]. Until the scan finishes, do not change your internet connection, VPN, proxy, or DNS. Changing the network during the scan can make its results inconsistent.

CDPIUI then:

1. Checks access to the sites without bypass settings.
2. Starts different Zapret2 strategies and repeats the requests.
3. Combines successful options into one config.
4. Tests the complete config.
5. Attempts to replace a conflicting option when necessary.

Open [Details] to see the current stage and estimated remaining time. An estimate may not be available at first. It is calculated from completed checks and becomes more accurate while the scan runs.

### While the scan is running

- Other CDPIUI network components are temporarily stopped.
- Internet access may be intermittent or unavailable.
- Do not manually start another bypass component because it can affect the results.
- You can leave the window in the background.

### To stop the scan

Select [Stop] and confirm. CDPIUI stops the temporary Zapret2 process and restores the components that were active before the scan.

Completed checks are not discarded. If a report can be created, you can open it immediately or later under [Previous scans](cdpiui://Tools/AutoConfig/Zapret2/Reports).

### Possible outcomes

- **Config selected successfully** — a common option was found and tested.
- **Preliminary config assembled** — the utility selected the best available option, but some targets may not have passed.
- **Scan stopped** — the report contains only the data collected before it was stopped.
- **Error** — open the result and diagnostic information. Earlier successful checks may still be available.

Next: [review the results and assembled config](cdpiui://Help/Zapret2PresetSelection/4ResultsAndPreset/).
