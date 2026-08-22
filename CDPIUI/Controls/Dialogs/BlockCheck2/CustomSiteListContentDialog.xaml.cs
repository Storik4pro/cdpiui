#nullable enable

using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using WinUI3Localizer;

namespace CDPIUI.Controls.Dialogs.BlockCheck2;

public sealed partial class CustomSiteListContentDialog : ContentDialog
{
    private readonly ILocalizer localizer = Localizer.Get();

    public CustomSiteListContentDialog(string? existingContent = null)
    {
        InitializeComponent();
        Title = Text(string.IsNullOrWhiteSpace(existingContent)
            ? "BlockCheck2CustomSiteListDialogCreateTitle"
            : "BlockCheck2CustomSiteListDialogEditTitle");
        PrimaryButtonText = Text("BlockCheck2CustomSiteListDialogSaveButton");
        CloseButtonText = Text("BlockCheck2CancelDialogButtonText");
        DefaultButton = ContentDialogButton.Primary;
        SiteListTextBox.Text = existingContent ?? string.Empty;
    }

    public string SiteListContent => SiteListTextBox.Text;

    private void ContentDialog_PrimaryButtonClick(
        ContentDialog sender,
        ContentDialogButtonClickEventArgs args)
    {
        BlockCheckTargetInputResult parsed = new BlockCheckTargetInputParser().Parse(
            SiteListTextBox.Text,
            new BlockCheckTargetInputOptions
            {
                Protocols = new HashSet<BlockCheckProtocol> { BlockCheckProtocol.TlsAuto },
                IpVersions = new HashSet<BlockCheckIpVersion> { BlockCheckIpVersion.IPv4 },
            });
        string[] errors = parsed.Issues
            .Where(issue => issue.Severity == BlockCheckIssueSeverity.Error)
            .Select(issue => issue.Message)
            .Distinct(StringComparer.CurrentCulture)
            .Take(3)
            .ToArray();
        if (parsed.Targets.Count > 0 && errors.Length == 0)
        {
            return;
        }

        ValidationInfoBar.Title = Text("BlockCheck2SiteListInvalidTitle");
        ValidationInfoBar.Message = errors.Length > 0
            ? string.Join(Environment.NewLine, errors)
            : Text("BlockCheck2SiteListInvalidMessage");
        ValidationInfoBar.IsOpen = true;
        args.Cancel = true;
    }

    private string Text(string resourceKey) => localizer.GetLocalizedString(resourceKey);
}
