# Changing the HOSTS list on your PC

Starting with version 0.5.0.0, the app has a built-in utility for replacing the HOSTS file on your PC.

The console window may flicker while using the utility.

### Why is this necessary?

Changing the HOSTS file can solve the following issues:

1. Fix connection to Discord servers located in Finland.
2. Restore access to GitHub servers, which are necessary for receiving app updates and proper Store functionality.

Detailed information about how the HOSTS file works and how to restore it in Windows can be found at: [Microsoft Support Forum](https://support.microsoft.com/en-us/topic/c2a43f9d-e176-c6f3-e4ef-3500277a6dae).

### How do I add addresses to the file?

*Note:* If the [Add addresses to the hosts file] menu item is unavailable, try repairing the file and then try again.

1. Open the [Hosts file utility]. You can do this on the [Utilities] page in the main application window.
2. Select [Add addresses to the hosts file].
3. Agree to grant administrator privileges to the `EditHostFile.exe` process. This is required, otherwise you won't be able to edit the file.
4. Make sure the replacement status at the top of the utility window displays "✅ The hosts file has been modified."
5. You are the best!

### How do I remove addresses from the file?

*Note:* If the [Remove addresses from the hosts file] menu item is unavailable, no changes have been made to the hosts file.

1. Open the [Hosts file utility]. You can do this on the [Utilities] page in the main application window.
2. Select [Remove addresses from the hosts file].
3. Grant administrator privileges to the `EditHostFile.exe` process. This is required, otherwise you won't be able to edit the file.
4. Ensure that the replacement status at the top of the utility window is displayed as "❎ The hosts file is not modified."
5. Well done!

### How do I restore a file?

*Note:* If the [Restore original hosts file from backup] menu item is unavailable, then no changes have been made to the hosts file.

1. Open the Hosts file utility. You can do this on the [Utilities] page in the main application window.
2. Select [Restore original hosts file from backup].
3. Grant administrator privileges to the `EditHostFile.exe` process. This is required, otherwise you won't be able to restore the file.
4. Ensure that the replacement status at the top of the utility window is displayed as "❎ The hosts file is not modified." The restore button should now be grayed out.
5. Congratulations!

## Possible Errors

Antivirus software may mark the `EditHostFile.exe` file as malicious and delete it. If the file is deleted, the application will return an error similar to `APPLICATION_DAMAGED_NEED_REPAIR` when attempting to edit the hosts file.

In this case, you can copy the `EditHostFile.exe` file from the latest Portable release to the application's working directory next to `CDPIUI.exe`. For the MSI version, simply perform a restore.

For manual debugging, you can run `EditHostFile.exe` in a terminal with administrator privileges and monitor the console output.

| Utility Launch Flag | Description |
| -------------------- | -------- |
| /add | Add addresses to the hosts file |
| /remove | Remove addresses from the hosts file |
| /recover | Restore addresses from the hosts.bak file |

## For developers

The utility includes a hosts file from the [zapret-discord-youtube](https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/refs/heads/main/.service/hosts) repository. Any changes to this file will require a rebuild of the utility (for security reasons).

Added addresses are marked with the `[CDOM-B]` tag at the beginning and `[CDOM-E]` tag at the end of the list to simplify their subsequent deletion and editing.