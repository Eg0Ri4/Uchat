using System;
using System.ComponentModel;

namespace client.Models
{
    public class ChatMessage : INotifyPropertyChanged
    {
        private string _messageText = "";
        private string _time = DateTime.Now.ToShortTimeString();
        private bool _isIncoming;
        private bool _isDeleted;
        private bool _isEdited;

        public string MessageText
        {
            get => _messageText;
            set
            {
                if (_messageText != value)
                {
                    _messageText = value;
                    OnPropertyChanged(nameof(MessageText));
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        public string Time
        {
            get => _time;
            set
            {
                if (_time != value)
                {
                    _time = value;
                    OnPropertyChanged(nameof(Time));
                }
            }
        }

        public bool IsIncoming
        {
            get => _isIncoming;
            set
            {
                if (_isIncoming != value)
                {
                    _isIncoming = value;
                    OnPropertyChanged(nameof(IsIncoming));
                    OnPropertyChanged(nameof(BubbleColor));
                }
            }
        }

        public bool IsDeleted
        {
            get => _isDeleted;
            set
            {
                if (_isDeleted != value)
                {
                    _isDeleted = value;
                    OnPropertyChanged(nameof(IsDeleted));
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        public bool IsEdited
        {
            get => _isEdited;
            set
            {
                if (_isEdited != value)
                {
                    _isEdited = value;
                    OnPropertyChanged(nameof(IsEdited));
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        public string BubbleColor => IsIncoming
    ? "#B2222222"  // темний сірий з прозорістю ~0.7
    : "#B25E2C2C"; // бордовий з прозорістю ~0.7


        public string DisplayText =>
            IsDeleted ? "Message deleted" :
            IsEdited  ? MessageText + " (edited)" :
                        MessageText;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
