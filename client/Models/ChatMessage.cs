using System;

namespace client.Models
{
    public class ChatMessage
    {
        public string MessageText { get; set; } = "";
        public string Time { get; set; } = DateTime.Now.ToShortTimeString();
        public bool IsIncoming { get; set; }

        public string BubbleColor => IsIncoming ? "#333" : "#660066";
    }
}
