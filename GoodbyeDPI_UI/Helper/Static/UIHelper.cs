using CDPI_UI.Helper.Items;
using CDPI_UI.Helper.LScript;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using WinUI3Localizer;
using Padding = System.Windows.Forms.Padding;

namespace CDPI_UI.Helper.Static
{
    public static class UIHelper
    {
        static UIHelper()
        {
        }

        public static IEnumerable<T> GetDescendantsOfType<T>(this DependencyObject start) where T : DependencyObject
        {
            return start.GetDescendants().OfType<T>();
        }

        public static IEnumerable<DependencyObject> GetDescendants(this DependencyObject start)
        {
            var queue = new Queue<DependencyObject>();
            var count1 = VisualTreeHelper.GetChildrenCount(start);

            for (int i = 0; i < count1; i++)
            {
                var child = VisualTreeHelper.GetChild(start, i);
                yield return child;
                queue.Enqueue(child);
            }

            while (queue.Count > 0)
            {
                var parent = queue.Dequeue();
                var count2 = VisualTreeHelper.GetChildrenCount(parent);

                for (int i = 0; i < count2; i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    yield return child;
                    queue.Enqueue(child);
                }
            }
        }

        static public UIElement FindElementByName(UIElement element, string name)
        {
            if (element.XamlRoot != null && element.XamlRoot.Content != null)
            {
                var ele = (element.XamlRoot.Content as FrameworkElement).FindName(name);
                if (ele != null)
                {
                    return ele as UIElement;
                }
            }
            return null;
        }

        // Confirmation of Action
        static public void AnnounceActionForAccessibility(UIElement ue, string annoucement, string activityID)
        {
            var peer = FrameworkElementAutomationPeer.FromElement(ue);
            peer.RaiseNotificationEvent(AutomationNotificationKind.ActionCompleted,
                                        AutomationNotificationProcessing.ImportantMostRecent, annoucement, activityID);
        }

        static public Color HexToColorConverter(string hexColor)
        {
            byte r = byte.Parse(hexColor.Substring(1, 2), NumberStyles.HexNumber);
            byte g = byte.Parse(hexColor.Substring(3, 2), NumberStyles.HexNumber);
            byte b = byte.Parse(hexColor.Substring(5, 2), NumberStyles.HexNumber);

            Logger.Instance.CreateDebugLog(nameof(UIHelper), $"{hexColor} is converted {r} {g} {b}");
            Color color = Color.FromArgb(255, r, g, b);

            return color;
        }

        static public SolidColorBrush HexToSolidColorBrushConverter(string hexColor)
        {
            Color color;
            if (string.IsNullOrEmpty(hexColor))
            {
                color = Color.FromArgb(0, 0, 0, 0);
            }
            else
            {
                color = HexToColorConverter(hexColor);
            }
            SolidColorBrush br = new SolidColorBrush(color);

            return br;
        }

        static public void GoBackWithParameter(object paramForPreviousPage, Frame frame)
        {
            var backStack = frame.BackStack;
            if (backStack == null || backStack.Count == 0)
                return; 

            var lastIndex = backStack.Count - 1;
            var lastEntry = backStack[lastIndex];

            var newEntry = new PageStackEntry(
                lastEntry.SourcePageType,
                paramForPreviousPage,
                lastEntry.NavigationTransitionInfo);

            backStack[lastIndex] = newEntry;

            if (frame.CanGoBack)
                frame.GoBack();
        }

        public enum SettingTileContentType
        {
            OnlyViewButton,
            EditViewButtons,
            ToggleSwitch,
            SiteListToggle,
            FullButton,
            OnlyTextContent,
            ComboBoxSelector
        }
        public class ComboBoxModel
        {
            public string DisplayName { get; set; }
            public string Id { get; set; }
        }

        public class SettingTileContentDefinition
        {
            public SettingTileContentType ContentType { get; set; }

            public string ActionGlyph { get; set; }

            public string Text { get; set; }

            public string VariableName { get; set; }
            public bool InitialToggleState { get; set; }
            public string PackId { get; set; }
            public string FileName { get; set; }

            public string EditFilePath { get; set; }
            public List<string> ViewParams { get; set; }
            public List<string> PrettyViewParams { get; set; }

            public string ClickId { get; set; }

            public List<ComboBoxModel> ComboBoxItems { get; set; }
            public string SelectedComboBoxItemId { get; set; }
        }

        public class SettingsTileItem
        {
            public string Title { get; set; }
            public bool ShowTopRectangle { get; set; }
            public IList<SettingTileContentDefinition> Contents { get; } = new List<SettingTileContentDefinition>();
        }

        public partial class SettingsTile : INotifyPropertyChanged
        {
            public string IconGlyph { get; set; } = "";
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
            public ObservableCollection<SettingsTileItem> Items { get; } = new ObservableCollection<SettingsTileItem>();

            public event PropertyChangedEventHandler PropertyChanged;
        }

        public enum ActionIds
        {
            None,
            ViewButtonClicked,
            EditButtonClicked,
            SwitchToggled,
            FullButtonElementClicked,
            ComboBoxSelectionChanged
        }

        public enum  TileType
        {
            Basic,
            QuickWidget
        }

        private static Thickness _Padding = new Thickness(15, 10, 15, 10);

        static public UIElement CreateSettingTile(
            SettingsTile preset, 
            Action<ActionIds, List<string>, SettingTileContentDefinition> executeAction, 
            TileType tileType = TileType.Basic,
            Thickness? padding = null)
        {
            ILocalizer localizer = Localizer.Get();

            if (padding == null) padding = _Padding;
            var rootStack = new StackPanel();

            foreach (var list in preset.Items)
            {
                var element = new SettingTileControlElement
                {
                    Title = list.Title,
                    ShowTopRectangle = list.ShowTopRectangle,
                    CardPadding = (Thickness)padding
                };

                var contentPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

                foreach (var def in list.Contents)
                {
                    switch (def.ContentType)
                    {
                        case SettingTileContentType.OnlyViewButton:
                            var _viewBtn = new Button { Padding = new Thickness(6) };
                            _viewBtn.Content = new FontIcon { Glyph = "\uE890", FontSize = 16 };
                            _viewBtn.Click += (s, e) =>
                            {
                                executeAction?.Invoke(ActionIds.ViewButtonClicked, [list.Title], def);
                            };

                            contentPanel.Children.Add(_viewBtn);
                            break;

                        case SettingTileContentType.EditViewButtons:
                            var editBtn = new Button { Padding = new Thickness(6) };
                            editBtn.Content = new FontIcon { Glyph = "\uE70F", FontSize = 16 };
                            editBtn.Click += (s, e) =>
                            {
                                executeAction?.Invoke(ActionIds.EditButtonClicked, null, def);
                            };

                            ToolTipService.SetToolTip(editBtn, localizer.GetLocalizedString("Edit")); 

                            var viewBtn = new Button { Padding = new Thickness(6) };
                            viewBtn.Content = new FontIcon { Glyph = "\uE890", FontSize = 16 };
                            viewBtn.Click += (s, e) =>
                            {
                                executeAction?.Invoke(ActionIds.ViewButtonClicked, [list.Title], def);
                            };

                            ToolTipService.SetToolTip(viewBtn, localizer.GetLocalizedString("ViewAppliedFlagsForSiteList"));

                            contentPanel.Children.Add(editBtn);
                            contentPanel.Children.Add(viewBtn);
                            break;

                        case SettingTileContentType.ToggleSwitch:
                            var toggle = new ToggleSwitch
                            {
                                IsOn = def.InitialToggleState
                            };
                            toggle.Toggled += (s, e) =>
                            {
                                executeAction?.Invoke(ActionIds.SwitchToggled, [toggle.IsOn.ToString()], def);
                                
                            };
                            contentPanel.Children.Add(toggle);
                            break;
                        case SettingTileContentType.FullButton:
                            element.ActionIconGlyph = "\uE8A7";
                            element.IsClickEnabled = true;

                            element.Click += () =>
                            {
                                executeAction?.Invoke(ActionIds.FullButtonElementClicked, null, def);
                            };
                            break;
                        case SettingTileContentType.OnlyTextContent:
                            var textBlock = new TextBlock
                            {
                                Text = def.Text,
                                TextWrapping = TextWrapping.WrapWholeWords
                            };
                            contentPanel.Children.Add(textBlock);
                            break;
                        case SettingTileContentType.ComboBoxSelector:
                            var comboBox = new ComboBox
                            {
                                ItemsSource = def.ComboBoxItems,
                                DisplayMemberPath = "DisplayName",
                                SelectedValuePath = "Id",
                                SelectedItem = def.ComboBoxItems.FirstOrDefault(x => x.Id == def.SelectedComboBoxItemId)
                            };
                            comboBox.SelectionChanged += (s, e) =>
                            {
                                executeAction?.Invoke(ActionIds.ComboBoxSelectionChanged, [((ComboBoxModel)comboBox.SelectedItem).Id], def);
                            };
                            contentPanel.Children.Add(comboBox);
                            break;
                    }
                }

                element.InnerContent = contentPanel;
                rootStack.Children.Add(element);
            }

            

            if (tileType == TileType.Basic)
            {
                var tile = new SettingTile
                {
                    IconGlyph = preset.IconGlyph,
                    Title = preset.Title,
                    Description = preset.Description,
                    InnerContent = rootStack
                };
                return tile;
            }
            else if (tileType == TileType.QuickWidget)
            {
                var tile = new QuickSettingWidget
                {
                    IconGlyph = preset.IconGlyph,
                    Title = preset.Title,
                    Description = preset.Description,
                    InnerContent = rootStack
                };
                return tile;
            }
            else
            {
                return null;
            }
        }

        public static StoreItemLargeButton CreateLargeButton(string storeId, string imageSource, string price, string title, string backgroundColor, Action<StoreItemLargeButton> action, string developer = "")
        {
            StoreItemLargeButton storeItemLargeButton;

            string eImageSource = LScriptLangHelper.ExecuteScript(imageSource);
            BitmapImage image = new BitmapImage(new Uri(eImageSource));

            Windows.UI.Color color = HexToColorConverter(backgroundColor);

            storeItemLargeButton = new StoreItemLargeButton
            {
                StoreId = storeId,
                CardTitle = title,
                CardDeveloper = developer,
                CardImageSource = image,
                CardPrice = price,
                CardBackgroundColor = color,
            };

            storeItemLargeButton.Click += action;

            return storeItemLargeButton;
        }

        public static StoreItemSmallButton CreateSmallButton(string storeId, string imageSource, string price, string title, string developer, string backgroundColor, Action<StoreItemSmallButton> action)
        {
            StoreItemSmallButton storeItemSmallButton;

            string eImageSource = LScriptLangHelper.ExecuteScript(imageSource);
            BitmapImage image = new BitmapImage(new Uri(eImageSource));

            SolidColorBrush solidColorBrush = UIHelper.HexToSolidColorBrushConverter(backgroundColor);

            storeItemSmallButton = new StoreItemSmallButton
            {
                StoreId = storeId,
                CardTitle = title,
                CardImageSource = image,
                CardPrice = price,
                CardDeveloper = developer,
                CardBackgroundBrush = solidColorBrush,
            };

            storeItemSmallButton.Click += action;

            return storeItemSmallButton;
        }

        public static string GetWindowName(string windowName)
        {
            return $"{windowName} — CDPI UI";
        }

        // https://github.com/microsoft/microsoft-ui-xaml/issues/934#issuecomment-2304875883

        public static class CleanUp
        {
            public static void FrameworkElement(FrameworkElement element)
            {
                var count = VisualTreeHelper.GetChildrenCount(element);
                for (var index = 0; index < count; index++)
                {
                    var child = VisualTreeHelper.GetChild(element, index);
                    if (child is FrameworkElement childElement)
                    {
                        FrameworkElement(childElement);
                    }
                }

                switch (element)
                {
                    case ItemsControl itemsControl:
                        itemsControl.ItemsSource = null;
                        break;
                    case ItemsRepeater itemsRepeater:
                        itemsRepeater.ItemsSource = null;
                        break;
                    case TabView tabView:
                        tabView.TabItemsSource = null;
                        break;
                }
            }
        }

        // https://github.com/FrozenAssassine/Fastedit/blob/v2.9.1/Fastedit/Helper/KeyHelper.cs
        public static bool IsKeyPressed(VirtualKey key)
        {
            return Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);
        }

        public static ApplicationTheme ConvertTheme(ElementTheme theme)
        {
            switch (theme)
            {
                case ElementTheme.Light: return ApplicationTheme.Light;
                case ElementTheme.Dark: return ApplicationTheme.Dark;
                case ElementTheme.Default:
                    var defaultTheme = new Windows.UI.ViewManagement.UISettings();
                    return defaultTheme.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background).ToString() == "#FF000000"
                        ? ApplicationTheme.Dark : ApplicationTheme.Light;

                default: return ApplicationTheme.Light;
            }
        }

    }
}

