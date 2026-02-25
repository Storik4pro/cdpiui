using CDPI_UI.Views.Store;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CDPI_UI.ViewModels
{
    internal partial class ViewConfigInKitModel : INotifyPropertyChanged
    {
        // Meta
        public string Guid { get; set; }
        public string PackId { get; set; }
        public string FileName { get; set; }

        // Properties
        public string TargetComponentId { get; set; }

        private string _displayName = string.Empty;
        public string DisplayName {
            get => _displayName;
            set => SetField(ref _displayName, value);
        }
        public List<string> UsedSiteLists { get; set; }
        public List<string> ExcludedSiteLists { get; set; }
        public string LastEditTime { get; set; }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
