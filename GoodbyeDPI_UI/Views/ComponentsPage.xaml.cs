using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.Themes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using GoodbyeDPI_UI.Helper;
using GoodbyeDPI_UI.ViewModels;
using static System.Net.Mime.MediaTypeNames;
using Microsoft.UI.Xaml.Documents;
using Application = Microsoft.UI.Xaml.Application;
using System.Windows.Input;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GoodbyeDPI_UI.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ComponentsPage : Page
    {
        public ObservableCollection<ComponentManager> Components { get; set; }
        private ComponentManager selectedComponent;
        public ICommand ReloadCommand { get; set; }
        
        public ComponentManager SelectedComponent
        {
            get => selectedComponent;
            set
            {
                if (selectedComponent != value)
                {
                    selectedComponent = value;
                    // Add property changed notification here if needed
                }
            }
        }

        public ComponentsPage()
        {
            this.InitializeComponent();

            Components = new ObservableCollection<ComponentManager>();

            CreateComponentList();
            if (!StateHelper.Instance.isCheckedComponentsUpdateComplete) CheckUpdates();

            if (StateHelper.Instance.lastComponentsUpdateError != "") ErrorHappens(StateHelper.Instance.lastComponentsUpdateError);

            // Subscribe to events
            foreach (var component in Components)
            {
                component.SelectedComponentChanged += Component_SelectedComponentChanged;
            }


            StateHelper.Instance.goodbyedpiSettings.ErrorHappens += ErrorHappens;
            StateHelper.Instance.zapretSettings.ErrorHappens += ErrorHappens;
            StateHelper.Instance.byedpiSettings.ErrorHappens += ErrorHappens;
            StateHelper.Instance.spoofdpiSettings.ErrorHappens += ErrorHappens;

            ReloadCommand = new RelayCommand(o => ReloadComponents());

            this.DataContext = this;

        }

        private void CreateComponentList()
        {
            
            List<ComponentManager> _components =
            [
                new ComponentManager
                {
                    Name = "GoodbyeDPI",
                    CurrentVersion = StateHelper.Instance.goodbyedpiSettings.currentVersion,
                    ServerVersion = StateHelper.Instance.goodbyedpiSettings.serverVersion,
                },
                new ComponentManager
                {
                    Name = "Zapret",
                    CurrentVersion = StateHelper.Instance.zapretSettings.currentVersion,
                    ServerVersion = StateHelper.Instance.zapretSettings.serverVersion,
                },
                new ComponentManager
                {
                    Name = "ByeDPI",
                    CurrentVersion = StateHelper.Instance.byedpiSettings.currentVersion,
                    ServerVersion = StateHelper.Instance.byedpiSettings.serverVersion,
                },
                new ComponentManager
                {
                    Name = "SpoofDPI",
                    CurrentVersion = StateHelper.Instance.spoofdpiSettings.currentVersion,
                    ServerVersion = StateHelper.Instance.spoofdpiSettings.serverVersion,
                },
            ];
            foreach (var componentManager in _components)
            {
                Components.Add(componentManager);
            }

        }

        private void ReloadComponents()
        {
            foreach (var component in Components)
            {
                component.SelectedComponentChanged -= Component_SelectedComponentChanged;
            }

            Components.Clear();
            CreateComponentList();

            foreach (var component in Components)
            {
                component.SelectedComponentChanged += Component_SelectedComponentChanged;

            }
        }

        private async Task CheckUpdates()
        {

            bool reloadNeed = false;
            if (await StateHelper.Instance.goodbyedpiSettings.IsUpdateAvailable())
            {
                reloadNeed = true;
            }
            if (await StateHelper.Instance.zapretSettings.IsUpdateAvailable())
            {
                reloadNeed = true;
            }
            if (await StateHelper.Instance.byedpiSettings.IsUpdateAvailable())
            {
                reloadNeed = true;
            }
            if (await StateHelper.Instance.spoofdpiSettings.IsUpdateAvailable())
            {
                reloadNeed = true;
            }
            StateHelper.Instance.isCheckedComponentsUpdateComplete = true;
            if (reloadNeed)
            {
                ReloadComponents();
            }
        }

        private void Component_SelectedComponentChanged(object sender, EventArgs e)
        {
            var selectedComponent = sender as ComponentManager;
            if (selectedComponent != null && selectedComponent.IsSelected)
            {
                foreach (var component in Components)
                {
                    if (component != selectedComponent)
                    {
                        component.IsSelected = false;
                    }
                }
                SelectedComponent = selectedComponent;
            }
        }

        private void ErrorHappens(string error)
        {
            Debug.WriteLine(error);
            ChangeTimeText($"Последняя проверка на наличие обновлений не удалась ({error})");
            StateHelper.Instance.lastComponentsUpdateError = error;
            
        }

        private void ChangeTimeText(string text)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                Run run = new Run
                {
                    Text = text,
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorAttentionBrush"]
                };



            });
        }


        private void OnSystemClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SystemPage));
            MainWindow.Instance.NavigateSubPage(typeof(SystemPage), SlideNavigationTransitionEffect.FromLeft);

        }

        private void OnComponentsClick(object sender, RoutedEventArgs e)
        {

        }

        private void Button_PointerEntered(object sender, PointerRoutedEventArgs e) 
        {
            var _sender = sender as Button;
        }

        private void Button_PointerExited(object sender, PointerRoutedEventArgs e)
        {
        }

        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            await CheckUpdates();
        }

        private void DeleteSomething(object obj)
        {
            try
            {
                Debug.WriteLine("DeleteSomething");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

    }
}
