# Select the best preset

Allows you to select the best preset for a component from already loaded/created presets.

To open this tool, go to the [Automatic Selection Tools] utility on the [Utilities] page of the main application window and select [Select the best preset automatically].

### Getting to Know the Utility Window

The window has three areas:
- **Select Settings.** Here you must specify the selection settings before running the test.
- **Results.** The results of the selection after completion will be shown here.
- **Debug Output.** The progress of the current selection will be shown here.

### Test Settings

Before starting the selection, make sure the following parameters are specified correctly:
- **Component.** Make sure the correct component is selected.
- **Test Type.** Select the test type. "Standard" takes less time but does not check for some types of blockages. "DPI Check" takes longer (each request is frozen to accurately detect blocking).
- **Which presets to test.** Select the list of presets to test. If you're unsure, leave "All presets" selected.

Also, configure the list of sites to test. You can open it by clicking [List of sites to test] at the bottom of the selection settings area.

The list has the following format:
```
SITE_NAME = Site_URL
```
There are two acceptable URL formats:
- **Standard.** URL starts with `https://`. Ping and HTTPS connection checks are performed.
- **Ping only.** URL starts with `PING:`. Only ping is performed.

Name – This is the site's display name in the log file and during the selection process. It doesn't affect the selection itself and can be any name, although it is not recommended to use characters other than the English alphabet.

### Starting the test

Once you've completed the setup, you can begin the selection process. To do this, click the [Start test] button and wait for it to complete. The selection process can take anywhere from a few minutes to an hour, depending on your provider and the number of presets being tested.

You can continue using your computer during the selection process, but applications using the network may generate errors.

You can end the selection process early by clicking [Stop test]. Information about the presets already tested will be displayed in the results window.

### Viewing results

Once the selection process is complete, review the results. Check whether the suggested preset is suitable for you and apply it.

### What should I do if none of the presets work?

- Open the [Troubleshooting] utility from the [Utilities] page of the main application window. Select "None of the presets allow me to access the websites I need" and wait for the test to complete. Follow the recommended steps and repeat the test.
- Download new preset sets from the Store. 
- - Contact support.