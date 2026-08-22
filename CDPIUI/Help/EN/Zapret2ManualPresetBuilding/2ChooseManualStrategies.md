# Choosing strategies

Open [Selection result]. Each row belongs to one specific site and connection type, such as HTTPS over IPv4.

### Choose a suitable option

1. Find the required site with search.
2. Show successful results and select [Best results first].
3. Compare the success rate first, then the response time.
4. Check [Without Zapret2]. If the site is already consistently available without bypass settings, it does not need a separate strategy.
5. Read the explanation before using a row with a warning icon. The strategy may have passed only some related connection types.

The most reliable candidate passes every repeated attempt. A result of `5/5 (100%)` is generally stronger evidence than `1/1 (100%)` because it was repeated several times.

### Retest the selected strategy

- [Test with CURL] repeats the automated requests and updates the row result.
- [Manual component test] temporarily starts the selected strategy. While it is running, open the site in your normal browser or application, then stop the test.

A manual browser check is particularly important when the automated request succeeds but the complete service still does not work.

### Choose a scope

- [Add for site] applies the strategy only to the selected site with the same connection type.
- [Add for site list] applies it to the entire related TXT list with the same connection type.

Use a list scope only when the strategy works for every required site in that list. If the sites require different options, add each one separately with [Add for site].

The same strategy cannot be added twice to the same scope. After adding it, the strategy appears under [Preset builder], and the config preview updates automatically.

### For advanced users

The automated check repeats a specific network request, not the complete behavior of a browser. It does not prove that additional addresses, JavaScript, video, or sign-in will work. Use both the request results and a manual service test before making the final choice.

Next: [review scopes, order, and warnings](cdpiui://Help/Zapret2ManualPresetBuilding/3OrganizeManualPreset/).
