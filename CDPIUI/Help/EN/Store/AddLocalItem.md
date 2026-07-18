# Adding an item from a local package

Starting with version 0.3.0.0, the Store supports manual installation and updating of items.

### Why is this necessary?

Local installation of items and updates can be useful if:

- For some reason, the app can't access the Store server to download and update items.
- You want to install an older version of an item that isn't available through the Store.
- The item you want to install isn't currently available in the Store.
- You want to deploy the app locally as quickly as possible, bypassing the Store.

### Is it safe?

Even though all packages undergo mandatory certification and the app verifies the integrity of the package signature, you shouldn't let your guard down.
Even a signed package can contain malware.

Only install a package if you are completely confident in its security and trust the provider.

### What types of packages are there?

- **CDPISIGNEDPACK** — a standard, signed package. It can contain an add-on, component, or preset set.
- **CDPICONFIGPACK** — a package designed for distributing preset sets. It is unsigned. It can only contain a preset set and has limitations on the supported file types when unpacking.
- **CDPIPATCH** — an application update package. It is signed. Contains the files necessary for updating the application.

### How to install a local package?

You can install an item from a local package in several ways.

1. Drag the package onto the application shortcut.
2. Right-click the package file and select [Open with]. Then specify the path to the application executable file, `CDPIUI.exe`.
3. Open the Store and go to the [Settings] page. Select [Add item from a local package]. Agree to the risks.

After performing one of the above steps, a dialog will open prompting you to install the item. Select [Install] and wait for the installation to complete.