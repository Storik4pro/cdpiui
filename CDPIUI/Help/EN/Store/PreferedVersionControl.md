# Preferred version control

Starting with version 0.3.0.0, the Store has the ability to change the preferred server for receiving item and app updates.

### What is this used for?

The app constantly contacts the server to obtain the following information:

- Availability of app updates.
- Availability of updates for individual items downloaded from the Store.
- The latest version of the Store database.

**[!] Please note.** You can completely disable checking for updates. To do this, go to [Settings] and uncheck the corresponding boxes in the [Notifications] group.

The Store also uses the server to download new items to your device.

### What does changing the preferred server affect?

Changing the preferred server forces the app to check for and download updates from a different server. However, some items may still be located on external servers, regardless of the app settings.

### Should I change the preferred server?

You should change your preferred server if:

- The download speed from the current server is extremely slow.
- The app cannot access the current server. For example: updating the Store database failed with an SSL error; checking for app updates failed with an `UNEXPECTED_STATUS_CODE` error, etc.
- When trying to download an item, you repeatedly encounter network-related errors (`HTTP_REQUEST_EXCEPTION`, etc.) **DO NOT CONFUSE** those related to write operations (`IO_GENERIC`, etc.)

If you are not experiencing any issues with either the Store or app updates, then you should not change your preferred server. *"If it works, don't touch it!"*

### How do I change my preferred server?

To change your preferred server, follow these simple steps:

- Open the Component Store (you can do this through the [Utilities] page in the main application window).
- Go to the [Settings] page of the Store.
- Switch the [Preferred version control] setting to the desired option.
- Restart the app: changes will take effect only after restarting.

### What version controls are available for the Store?

Currently, the Store has a repository on public GitHub and GitLab servers. The number of available servers may change in the future.

- **GitHub** Default repository. Links to the original repositories located on GitHub. May be unavailable to some users.
- **GitLab** Backup repository. Links to backups of the original repositories on GitLab. Backups are maintained by the app developer and are not directly related to the authors of the elements.

Up-to-date information on available servers and their addresses can be found in the app developer's official Telegram channel.