using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CDPI_UI.ViewModels
{
    public class ViewTelegramDataCenterModel : INotifyPropertyChanged
    {
        public string Guid { get; set; }

        private string _number = string.Empty;
        public string Number
        {
            get => _number;
            set => SetField(ref _number, value);
        }

        private string _ip = string.Empty;
        public string Ip
        {
            get => _ip;
            set => SetField(ref _ip, value);
        }

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
