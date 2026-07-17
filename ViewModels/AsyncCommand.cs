using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ASTEM_DB.ViewModels
{
    public sealed class AsyncCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private bool _isRunning;

        public AsyncCommand(Func<Task> execute)
        {
            _execute = execute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => !_isRunning;

        public async void Execute(object? parameter)
        {
            if (_isRunning)
                return;

            try
            {
                _isRunning = true;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
                await _execute();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Command failed: {ex}");
            }
            finally
            {
                _isRunning = false;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
