## Welcome to CDPI UI repo!

Here some rules and tips for you to make contributing easier

### Localization
- On EN localization ready settings file for components called "Config", on RU localization "Пресет".

#### Add localization for new language
- Create resource files with `.resw` extension in `CDPIUI/Strings/<loc_code>`. `<loc_code>` must follow format like `en-us` or `ru`. You must set "Build Action" as "Content" and set "Copy if never" flag.
- Create `.md` file for localize ELUA `CDPIUI/ELUA/<loc_code>`. `<loc_code>` must follow Store-like format like `EN` or `RU`.
- Create `.md` files for localize WIKI `CDPIUI/Help/<loc_code>` `<loc_code>` must follow Store-like format like `EN` or `RU`.
- To localize Store visit [Store repo](https://github.com/Sotrik4pro/CDPIUI-Store).
- Add key `<loc_code>` to all `.resw` files with localized localization name for existed localizations.
- In `CDPIUI.Helper.UserExperience` add key to `Languages` ObservableCollection
- In `CDPIUI.App` add pair in `InitializeLocalizer`

To localize TrayIcon:
- Add dict to `CDPIUI.TrayIcon.Helper.Locale.LocaleHelper`.
- In `CDPIUI.TrayIcon.Helper.Locale.LocaleHelper.GetLocaleString` add pair for `<loc_code>` in ISO format with two letters.

### Add a feature 

#### Before we start
If feature you added changing existing feature or application core follow "Edit an application code"
Otherwise, follow "Create new AddOn"


#### Project structure 
>Make sure you use correct naming. My grammar is too bad, sorry :( 
```text
cdpiui/
├── .github/                           # GitHub workflows and CI/CD configurations
├── CDPIUI/                            # GUI source code
|   ├── Assets/                        # Images of application
|   ├── Commands/                      # Handlers of commands
|   |   └── CommandsUriMapper.cs       # Mapper for external cdpiui:// links
|   ├── ConditionalLaunch/             # Localized commands catalogues for ConditionalLaunch utility
|   ├── Controls/                      # Custom controls
|   |   └── Dialogs/                   # Custom dialogs
|   ├── Converters/                    # UI converters (e.g. BoolToVisibilityConverter)
|   ├── ELUA/                          # Localized ELUA .md files
|   ├── Extensions/                    # Extensions
|   ├── Help/                          # Localized wiki .md files
|   ├── Helper/                        # UI helpers (classes, who required localization or XAML elements)
|   ├── Properties/                    # VisualStudio 2022 project settings
|   ├── Strings/                       # UI elements localization
|   ├── Template/                      # Template files to copy to build directory
|   ├── ViewModels/                    # UI ViewModels and associated helpers (build-in)
|   ├── Views/                         # Pages. Store pages for window in folders with same name as window name
|   └── Windows/                       # Windows. IMPORTANT! Namespace for windows is CDPIUI.*, DO NOT use CDPIUI.Windows.*
|   ├── App.xaml                       # Main styles
|   ├── App.xaml.cs                    # Main GUI application code
|   └── Program.cs                     # Entry point of application
├── CDPIUI.AddOns                      # AddOns. Store AddOns files in same as AddOn name folder
├── CDPIUI.Core/                       # Core application/business logic
│   ├── Basic/                         # Basic core abstractions and utilities
│   ├── Communication/                 # Core communication infrastructure
│   ├── ComponentServices/             # Services responsible for application components
│   ├── Data/                          # Core data structures/services
│   ├── Features/                      # Core feature implementations
│   ├── JSON/                          # JSON-related functionality
│   ├── LScript/                       # LScript subsystem
│   ├── Proxy/                         # Proxy-related core functionality
│   ├── Security/                      # Security-related services and utilities
│   ├── Store/                         # Core store subsystem
│   ├── System/                        # Operating-system/system integration
│   ├── Application.cs                 # Core application abstraction/state
│   ├── CoreEvents.cs                  # Shared core application events
│   ├── SettingsManager.cs             # Application settings management
│   ├── State.cs                       # Core application state
│   └── CDPIUI.Core.csproj             # Core project definition
├── CDPIUI.Shared/                     # Code shared between GUI, Core and helper applications
│   ├── Basic/                         # Common/basic helpers
│   ├── ComponentsTask/                # Shared component-task infrastructure
│   ├── ConditionalLaunch/             # Shared ConditionalLaunch implementation
│   ├── Exceptions/                    # Shared exception types
│   ├── Extentions/                    # Shared extension methods
│   │                                  # NOTE: repository currently spells this "Extentions"
│   ├── Logger/                        # Shared logging infrastructure
│   ├── Migration/                     # Data/settings migration functionality
│   ├── Models/                        # Models shared across projects
│   ├── Pipe/                          # Inter-process pipe communication
│   ├── PrettyErrorConvertionService/  # User-friendly error conversion
│   │                                  # NOTE: repository currently uses "Convertion"
│   ├── Secrets/                       # Secret/value handling infrastructure
│   ├── System/                        # Shared system/OS helpers
│   ├── SharedConstants.cs             # Constants shared across applications
│   ├── Utils.cs                       # General shared utilities
│   └── CDPIUI.Shared.csproj           # Shared library project definition
│
├── CDPIUI.TrayIcon/                   # System-tray companion application
│   ├── Assets/                        # Tray application assets/icons
│   ├── ConditionalLaunch/             # Tray ConditionalLaunch integration
│   ├── Controls/                      # Tray UI controls
│   ├── Forms/                         # Windows Forms used by tray application
│   ├── Helper/                        # Tray-specific helpers
│   ├── Properties/                    # Project properties/resources
│   ├── Program.cs                     # Tray application entry point
│   ├── app.manifest                   # Tray executable manifest
│   └── CDPIUI.TrayIcon.csproj         # Tray application project definition
│
├── EditHostFile/                      # Elevated helper used for hosts-file modification
│   ├── Data/                          # Helper data/resources
│   ├── Properties/                    # Project properties
│   ├── App.config                     # Application configuration
│   ├── Program.cs                     # Helper entry point
│   ├── app.manifest                   # Executable/UAC manifest
│   └── EditHostFile.csproj            # EditHostFile project definition
│
├── NewSetup/                          # WiX-based application installer
│   ├── Resources/                     # Installer resources
│   ├── NewSetup.wixproj               # WiX installer project
│   ├── NewSetupFiles.wxs              # Installed file/component definitions
│   ├── Product.wxs                    # Main installer/product definition
│   └── UI.wxs                         # Installer UI definition
│
├── Update/                            # Standalone updater application
│   ├── Properties/                    # Updater project properties
│   ├── App.config                     # Updater configuration
│   ├── Form1.cs                       # Updater Windows Form logic
│   ├── Form1.Designer.cs              # Generated Windows Form layout
│   ├── Form1.resx                     # Form resources
│   ├── InstallHelper.cs               # Update/install helper logic
│   ├── Program.cs                     # Updater entry point
│   ├── packages.config                # NuGet package configuration
│   └── Update.csproj                  # Updater project definition
│
├── .editorconfig                      # Repository-wide editor/code formatting rules
├── .gitattributes                     # Git attribute configuration
├── .gitignore                         # Git ignore rules
├── CDPI.sln                           # Main Visual Studio solution
├── LICENSE.txt                        # Project license
└── README.md                          # Repository user-friendly README
```


#### Edit an application code

Just don't broke everything. DO NOT edit application entry point, uri handler or any other existed code without reason. If something not implemented best way is create something new except of editing something.
If it works – don't touch!

#### Create new AddOn

Application supports external and internal AddOns. External and internal AddOns needed some application code to work.

Difference is how much code you need to add in application.
External AddOns (like GoodCheck) usually has main (worker) part as compiled executable. In application you need to implement some code and UI to control AddOn settings and handle it work.

Internal AddOns fully stored in CDPIUI.AddOns project. One AddOn – one project folder.
In main application you must add some XAML UI or UI helpers (for localization, as example)

CDPIUI.AddOns referenced from CDPIUI.Shared and CDPIUI.Core. It means AddOn can access application core and shared utils directly (if requested class/method is public)

### Apply changes to CDPIUI.TrayIcon

Are you really want it? Do it!
Just don't break everything.

Also, keep in mind – TrayIcon must be fast and responsible. This is the main rule. 

### Apply changes to CDPIUI.Shared

If you want fix something – make sure your fix won't break anything.
If you want add something – make sure you really want it. Shared – is a library for CDPIUI (GUI) and CDPIUI.TrayIcon (Back). If something used only in GUI, adding it to Shared isn't a good idea.

### Apply changes to CDPIUI.Core

Make sure function you added stored in right directory (to keep structure understandable)

### Add new component

You can request component addition to CDPIUI Store in official developer's social. All links can be found in application telegram channel. 

For add new component manually, create item in Store repo, add component Id to CDPIUI.Core.Store.Data.HardcodedItemIds. Then add locale key (view Localization paragraph) with id with localized name (EN only supported for component names)

### Add new config kit/subscription 

You can request config kit/subscription addition to CDPIUI Store in official developer's social. All links can be found in application telegram channel. 

For add new component manually, create item in Store repo. No code needed.

