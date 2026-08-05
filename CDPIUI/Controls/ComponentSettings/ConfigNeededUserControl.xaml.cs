using CDPIUI.Commands;
using CDPIUI.Controls.WindowControls;
using CDPIUI.Core.Store.Data;
using CDPIUI.Helper;
using CDPIUI.Shared.Extentions;
using CDPIUI.ViewModels;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using WinUI3Localizer;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CDPIUI.Controls.ComponentSettings
{
    public sealed partial class ConfigNeededUserControl : UserControl
    {
        public static readonly DependencyProperty ClickCommandProperty =
            DependencyProperty.Register(
                nameof(ClickCommand),
                typeof(ICommand),
                typeof(ConfigNeededUserControl),
                new PropertyMetadata(null)
            );

        public ICommand ClickCommand
        {
            get => (ICommand)GetValue(ClickCommandProperty);
            set => SetValue(ClickCommandProperty, value);
        }

        public static readonly DependencyProperty ClickCommandParameterProperty =
            DependencyProperty.Register(
                nameof(ClickCommandParameter),
                typeof(object),
                typeof(ConfigNeededUserControl),
                new PropertyMetadata(null)
            );

        public object ClickCommandParameter
        {
            get => GetValue(ClickCommandParameterProperty);
            set => SetValue(ClickCommandParameterProperty, value);
        }

        private readonly ObservableCollection<AvailableConfigCreationActionModel> Actions = [];

        private readonly Dictionary<Components, List<AvailableConfigCreationActios>> ActionModels = new()
        {
            { Components.GoodbyeDPI, [AvailableConfigCreationActios.CreateAutomatically] },
            { Components.Zapret, [AvailableConfigCreationActios.CreateAutomatically, AvailableConfigCreationActios.ViewInStore] },
            { Components.Zapret2, [AvailableConfigCreationActios.ViewInStore] },
            { Components.ByeDPI, [AvailableConfigCreationActios.CreateAutomatically] },
            { Components.TgWsProxy, [AvailableConfigCreationActios.CreateFromTemplate] },
        };

        private readonly ILocalizer localizer = Localizer.Get();

        public ConfigNeededUserControl()
        {
            InitializeComponent();

            AvailableActionsListView.ItemsSource = Actions;
            //CreateTiles();
        }

        public string StoreId
        {
            get { return (string)GetValue(StoreIdProperty); }
            set { 
                SetValue(StoreIdProperty, value);
                CreateTiles();
            }
        }

        public static readonly DependencyProperty StoreIdProperty =
            DependencyProperty.Register(
                nameof(StoreId), typeof(string), typeof(ConfigNeededUserControl), new PropertyMetadata(string.Empty)
            );

        private void CreateTiles()
        {
            Actions.Clear();
            Debug.WriteLine(StoreId);

            var cmpActions = ActionModels.ContainsKey(HardcodedItemIds.ComponentIds.FirstOrDefault(x => x.Value == StoreId).Key) 
                ? ActionModels[HardcodedItemIds.ComponentIds.FirstOrDefault(x => x.Value == StoreId).Key] 
                : [];

            if (cmpActions.Contains(AvailableConfigCreationActios.CreateFromTemplate))
            {
                Actions.Add(new AvailableConfigCreationActionModel
                {
                    Name = localizer.GetLocalizedString("CreateFromTemplate"),
                    Description = localizer.GetLocalizedString("CreateFromTemplateDescription"),
                    IconUri = UIHelper.GetUriFromString("ms-appx:///Assets/Icons/Edit.png"),
                    ActionIconGlyph = "\uE76C",
                    Action = AvailableConfigCreationActios.CreateFromTemplate
                });
            }
            if (cmpActions.Contains(AvailableConfigCreationActios.CreateAutomatically))
            {
                Actions.Add(new AvailableConfigCreationActionModel
                {
                    Name = localizer.GetLocalizedString("CreateAutomatically"),
                    Description = localizer.GetLocalizedString("CreateAutomaticallyDescription"),
                    IconUri = UIHelper.GetUriFromString("ms-appx:///Assets/Icons/GoodCheck.ico"),
                    ActionIconGlyph = "\uE8A7", 
                    Action = AvailableConfigCreationActios.CreateAutomatically
                });
            }

            Actions.Add(new AvailableConfigCreationActionModel
            {
                Name = localizer.GetLocalizedString("ConfigImportUtilWindowTitle"),
                Description = localizer.GetLocalizedString("ConfigImportUtilDescription"),
                IconUri = UIHelper.GetUriFromString("ms-appx:///Assets/Icons/Import.ico"),
                ActionIconGlyph = "\uE8A7", 
                Action = AvailableConfigCreationActios.ImportFromFile
            });

            if (cmpActions.Contains(AvailableConfigCreationActios.ViewInStore))
            {
                Actions.Add(new AvailableConfigCreationActionModel
                {
                    Name = localizer.GetLocalizedString("GetInStore"),
                    Description = localizer.GetLocalizedString("GetInStoreDescription"),
                    IconUri = UIHelper.GetUriFromString("ms-appx:///Assets/Icons/Store.png"),
                    ActionIconGlyph = "\uE8A7", 
                    Action = AvailableConfigCreationActios.ViewInStore
                });
            }

            Actions.Add(new AvailableConfigCreationActionModel
            {
                Name = localizer.GetLocalizedString("CreateConfigManually"),
                Description = localizer.GetLocalizedString("CreateConfigManuallyDescription"),
                IconUri = UIHelper.GetUriFromString("ms-appx:///Assets/Icons/Edit.png"),
                ActionIconGlyph = "\uE8A7",
                Action = AvailableConfigCreationActios.CreateManually
            });
            
        }

        private void SettingsCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is SettingsCard el && el.Tag is AvailableConfigCreationActios action)
            {
                ClickCommandParameter = action;
                if (ClickCommand != null && ClickCommand.CanExecute(ClickCommandParameter))
                {
                    ClickCommand.Execute(ClickCommandParameter);
                }

                switch (action)
                {
                    case AvailableConfigCreationActios.ViewInStore:
                        {
                            CommandsHandler.HandleCommand("cdpiui://Store");
                            break;
                        }
                    case AvailableConfigCreationActios.CreateManually:
                        {
                            CommandsHandler.HandleCommand($"cdpiui://Tools/CreateConfig/{StoreId}");
                            break;
                        }
                    case AvailableConfigCreationActios.CreateAutomatically:
                        {
                            CommandsHandler.HandleCommand($"cdpiui://Tools/AutoConfig/{StoreId}");
                            break;
                        }
                    case AvailableConfigCreationActios.CreateFromTemplate:
                        {
                            break;
                        }
                    case AvailableConfigCreationActios.ImportFromFile:
                        {
                            CommandsHandler.HandleCommand($"cdpiui://Tools/ImportConfig/{StoreId}");
                            break;
                        }
                }
            }
        }
    }
}
