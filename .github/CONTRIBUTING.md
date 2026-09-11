## Welcome to CDPI UI repo!

Here some rules and tips for you to make contributing easier

### Localization rules
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

