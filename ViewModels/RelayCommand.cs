using System;
using System.Diagnostics;
using System.Windows.Input;

namespace ASTEM_DB.ViewModels
{
    public sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;

        public RelayCommand(Action execute)
        {
            _execute = execute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            try
            {
                _execute();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Command failed: {ex}");
            }
        }
    }
}
