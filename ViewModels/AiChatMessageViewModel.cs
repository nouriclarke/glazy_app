using ReactiveUI;

namespace ASTEM_DB.ViewModels
{
    public class AiChatMessageViewModel : ViewModelBase
    {
        private readonly bool _isFromUser;
        private string _message = string.Empty;

        public AiChatMessageViewModel(string sender, string message)
        {
            _isFromUser = sender == "You";
            Message = message;
        }

        public string Sender => Localization.Get(_isFromUser ? "SenderYou" : "SenderGlazy");

        public string Message
        {
            get => _message;
            set => this.RaiseAndSetIfChanged(ref _message, value);
        }

        public bool IsFromUser => _isFromUser;
        public bool IsFromAssistant => !IsFromUser;

        public void RefreshLocalization()
        {
            this.RaisePropertyChanged(nameof(Sender));
        }
    }
}
