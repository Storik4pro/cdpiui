using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GoodbyeDPI_UI.ViewModels
{
    public class RelayCommand : ICommand
    {
        private Action<object> execute;
        private Func<object, bool> canExecute;
        public event EventHandler CanExecuteChanged;
        public RelayCommand(Action<object> executeAction, Func<object, bool> canExecuteFunc = null)
        {
            execute = executeAction;
            canExecute = canExecuteFunc;
        }
        public bool CanExecute(object parameter) => canExecute == null || canExecute(parameter);
        public void Execute(object parameter) => execute(parameter);
    }
}
