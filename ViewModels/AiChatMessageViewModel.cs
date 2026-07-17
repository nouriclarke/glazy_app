using ReactiveUI;

namespace ASTEM_DB.ViewModels
{
    public class AiChatMessageViewModel : ViewModelBase
    {
        private string _sender = string.Empty;
        private string _message = string.Empty;

        public AiChatMessageViewModel(string sender, string message)
        {
            Sender = sender;
            Message = message;
        }

        public string Sender
        {
            get => _sender;
            set => this.RaiseAndSetIfChanged(ref _sender, value);
        }

        public string Message
        {
            get => _message;
            set => this.RaiseAndSetIfChanged(ref _message, value);
        }
    }
}
