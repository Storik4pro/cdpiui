using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using WinUI3Localizer;

namespace CDPIUI.ViewModels;

public sealed class AppFeatureViewModel
{
    public string Title { get; set; }
    public string Description { get; set; }
    public ImageSource ImageSource { get; set; }
    public IReadOnlyList<AppFeatureLinkViewModel> Links { get; set; } = [];
    public bool IsNew { get; set; }

    public Visibility BadgeVisibility => !IsNew
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility DescriptionVisibility => string.IsNullOrWhiteSpace(Description)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility ImageVisibility => ImageSource == null
        ? Visibility.Collapsed
        : Visibility.Visible;
}

public sealed class AppFeatureLinkViewModel
{
    public string DisplayText { get; set; }
    public string URI { get; set; }
}

public static class AppFeaturesCatalog
{
    private static readonly AppFeatureDefinition[] Definitions =
    [
        new(
            "AppFeatureZapret2Support",
            "AppFeatureZapret2SupportDescription",
            null,
            true,
            new AppFeatureLinkDefinition("AppFeatureReviewZapret2Support", "cdpiui://Store/Catalog/CSZTBN062")),
        new(
            "AppFeatureConditionalRun",
            "AppFeatureConditionalRunDescription",
            null,
            true,
            new AppFeatureLinkDefinition("AppFeatureReviewConditionalRun", "cdpiui://Tools/ConditionalLaunch")),
        new(
            "AppFeatureShareConfigs",
            "AppFeatureShareConfigsDescription",
            null,
            true),
        new(
            "AppFeatureLikeYouWantTitle",
            "AppFeatureLikeYouWantDescription",
            "ms-appx:///Assets/Welcome/ThemeView.png",
            true,
            new AppFeatureLinkDefinition("AppFeatureReviewThemeSettings", "cdpiui://Main/Settings/Personalization")),
        new(
            "AppFeatureStoreTitle",
            "AppFeatureStoreDescription",
            null,
            false,
            new AppFeatureLinkDefinition("AppFeatureStoreOpenLink", "cdpiui://Store")),
        new(
            "AppFeatureAutoConfigTitle",
            "AppFeatureAutoConfigDescription",
            null,
            false,
            new AppFeatureLinkDefinition("AppFeatureAutoConfigOpenLink", "cdpiui://Tools/AutoConfig")),
        new(
            "AppFeatureConfigToolsTitle",
            "AppFeatureConfigToolsDescription",
            null,
            false,
            new AppFeatureLinkDefinition("AppFeatureConfigEditorOpenLink", "cdpiui://Tools/ConfigEditor"),
            new AppFeatureLinkDefinition("AppFeatureConfigImportOpenLink", "cdpiui://Tools/ImportConfig")),
        new(
            "AppFeatureTroubleshootingTitle",
            "AppFeatureTroubleshootingDescription",
            null,
            false,
            new AppFeatureLinkDefinition("AppFeatureTroubleshootingOpenLink", "cdpiui://Tools/Troubleshooting"))
    ];

    public static IReadOnlyList<AppFeatureViewModel> CreateLocalized(ILocalizer localizer) =>
        Definitions.Select(definition => new AppFeatureViewModel
        {
            Title = localizer.GetLocalizedString(definition.TitleResourceKey),
            Description = definition.DescriptionResourceKey == null
                ? string.Empty
                : localizer.GetLocalizedString(definition.DescriptionResourceKey),
            ImageSource = definition.ImageUri == null
                ? null
                : new BitmapImage(new Uri(definition.ImageUri)),
            IsNew = definition.IsNew,
            Links = definition.Links.Select(link => new AppFeatureLinkViewModel
            {
                DisplayText = localizer.GetLocalizedString(link.DisplayTextResourceKey),
                URI = link.URI
            }).ToArray()
        }).ToArray();

    private sealed record AppFeatureDefinition(
        string TitleResourceKey,
        string DescriptionResourceKey,
        string ImageUri,
        bool IsNew = false,
        params AppFeatureLinkDefinition[] Links);

    private sealed record AppFeatureLinkDefinition(
        string DisplayTextResourceKey,
        string URI);
}
