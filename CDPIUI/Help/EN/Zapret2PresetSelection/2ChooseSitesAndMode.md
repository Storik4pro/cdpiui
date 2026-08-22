# Preparing for a scan

Preparation has three steps: choose the sites, choose a scan mode, and review the advanced settings.

### 1. Add sites

Select [Add a site list]. You can choose a list installed in CDPIUI, add a TXT file, drag a TXT file into the window, or create your own list.

Put one address on each line of a custom list or TXT file. For example:

```text
example.com
https://example.org/video
```

Lines that begin with `#` are comments and are not tested.

If you enter only a domain, the utility tests the connection types selected below. If you enter a complete address beginning with `http://` or `https://`, the test follows that scheme.

### Choose how to process a list

Select one option for each list:

- **One strategy for the whole list** — the utility looks for one option that works for every site in the list. This produces a smaller config and is a good first choice for a ready-made themed list.
- **Select for every site** — different sites can use different strategies. This is useful for a mixed list, but the scan takes longer and the config may be larger.

If no common option works for a list, run another scan with **Select for every site**.

### 2. Choose a mode

- **Quick** — a minimal set of strategies and two attempts. Suitable for a short first scan.
- **Balanced** — an expanded set, three attempts, and an additional test of the complete config. Recommended in most cases.
- **Exhaustive** — the full available set, five attempts, and stronger stability checks. It may take much longer.

Start with Balanced. Use Exhaustive if the result is unstable or no suitable option is found.

### 3. Review the advanced settings

If you are unsure what a setting does, keep the recommended value or select [Restore recommended values].

Most modern sites require HTTPS checks. Testing only HTTP is not enough: a successful response or redirect does not mean that the secure page will open.

- **HTTPS (automatic TLS)** is closest to a normal secure connection.
- **TLS 1.2** can be added to test another common HTTPS option.
- **TLS 1.3 (exact)** tests TLS 1.3 specifically.
- **IPv4** is suitable for most connections.
- Enable **IPv6** only if your provider supplies a working IPv6 connection.

### For advanced users

- **Connection timeout** — how long to wait for a connection to the site.
- **Total request time** — the overall limit for one attempt. It cannot be shorter than the connection timeout.
- **Zapret2 startup wait** — the pause between starting a strategy and requesting the site.
- **Requests per domain** — how many times to repeat each check. More attempts reveal unstable results more reliably but make the scan considerably longer.
- **Continue after finding the best strategies** — test the remaining strategies in the selected mode. This increases scan time and does not include strategies from a more exhaustive mode.

On [Review settings], check the number of sites, connection types, and selected mode. The displayed duration is a conservative upper estimate, not an exact prediction.

Next: [start and monitor the scan](cdpiui://Help/Zapret2PresetSelection/3DuringPresetSelection/).
