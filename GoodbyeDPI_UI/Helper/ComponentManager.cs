using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using GoodbyeDPI_UI.ViewModels;

namespace GoodbyeDPI_UI.Helper
{

    public partial class ComponentManager : INotifyPropertyChanged
    {
        private bool isSelected;

        public string Name { get; set; }
        public string CurrentVersion { get; set; }
        public string ServerVersion { get; set; }

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected != value)
                {
                    isSelected = value;
                    OnPropertyChanged();
                    if (isSelected)
                    {
                        SelectedComponentChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        public ICommand UpdateCommand { get; set; }
        public ICommand OpenDirectoryCommand { get; set; }
        public ICommand ConfigureCommand { get; set; }
        public ICommand DeleteCommand { get; set; }

        public event EventHandler SelectedComponentChanged;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ComponentManager()
        {
            UpdateCommand = new RelayCommand(o => UpdateComponent());
            OpenDirectoryCommand = new RelayCommand(o => OpenDirectory());
            ConfigureCommand = new RelayCommand(o => ConfigureComponent());
            DeleteCommand = new RelayCommand(o => DeleteComponent());
        }

        private void UpdateComponent()
        {
            Debug.WriteLine(Name);
        }

        private void OpenDirectory()
        {
            
        }

        private void ConfigureComponent()
        {
            // Code to configure the component
        }

        private void DeleteComponent()
        {
            // Code to delete the component
        }
    }
}
