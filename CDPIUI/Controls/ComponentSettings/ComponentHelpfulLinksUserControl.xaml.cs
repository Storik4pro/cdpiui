using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using CDPIUI.Shared.Extentions;
using CDPIUI.Commands;
using CDPIUI.Controls.Dialogs.ComponentSettings;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Markup;


namespace CDPIUI.Controls.ComponentSettings
{
    public enum HelpfulLinkType
    {
        None = 0,
        EditConfig,
        CreateNewConfig,
        ConnectTgWsProxy,
        SetupProxy,
        OpenAutoselectionUtil,
    }

    [ContentProperty(Name = nameof(Items))]
    public sealed partial class ComponentHelpfulLinksUserControl : UserControl
    {
        public ObservableCollection<HelpfulLink> Items { get; } = [];

        public event RoutedEventHandler ConfigEditRequested;

        public ComponentHelpfulLinksUserControl()
        {
            InitializeComponent();
        }

        public string TargetComponentId
        {
            get => (string)GetValue(TargetComponentIdProperty);
            set => SetValue(TargetComponentIdProperty, value);
        }

        public static readonly DependencyProperty TargetComponentIdProperty = DependencyProperty.Register(
            nameof(TargetComponentId),
            typeof(string),
            typeof(ComponentHelpfulLinksUserControl),
            new PropertyMetadata(string.Empty));

        private async void SettingsCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is SettingsCard card && 
                card.Tag is HelpfulLinkType type)
            {
                switch (type)
                {
                    case HelpfulLinkType.CreateNewConfig:
                        CommandsHandler.HandleCommand($"cdpiui://Tools/CreateConfig/{TargetComponentId}");
                        break;
                    case HelpfulLinkType.EditConfig:
                        ConfigEditRequested?.Invoke(this, new RoutedEventArgs());
                        break;
                    case HelpfulLinkType.ConnectTgWsProxy:
                        ConnectTelegramProxyContentDialog dialog = new()
                        {
                            XamlRoot = this.XamlRoot
                        };
                        await dialog.ShowAsync();
                        break;
                    case HelpfulLinkType.SetupProxy:
                        CommandsHandler.HandleCommand($"cdpiui://Tools/Proxy");
                        break;
                    case HelpfulLinkType.OpenAutoselectionUtil:
                        CommandsHandler.HandleCommand($"cdpiui://Tools/AutoConfig/{TargetComponentId}");
                        break;
                }
            }
        }
    }

    public sealed class HelpfulLink : DependencyObject
    {
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(HelpfulLink),
            new PropertyMetadata(string.Empty));

        public bool IsVisible
        {
            get => (bool)GetValue(IsVisibleProperty);
            set => SetValue(IsVisibleProperty, value);
        }

        public static readonly DependencyProperty IsVisibleProperty = DependencyProperty.Register(
            nameof(IsVisible),
            typeof(bool),
            typeof(HelpfulLink),
            new PropertyMetadata(true));

        public IconElement Icon
        {
            get => (IconElement)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon),
            typeof(IconElement),
            typeof(HelpfulLink),
            new PropertyMetadata(null));

        public HelpfulLinkType HelpfulLinkType
        {
            get => (HelpfulLinkType)GetValue(HelpfulLinkTypeProperty);
            set => SetValue(HelpfulLinkTypeProperty, value);
        }

        public static readonly DependencyProperty HelpfulLinkTypeProperty = DependencyProperty.Register(
            nameof(HelpfulLinkType),
            typeof(HelpfulLinkType),
            typeof(HelpfulLink),
            new PropertyMetadata(HelpfulLinkType.None));
    }
}
