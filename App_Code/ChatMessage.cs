using System;
using System.Collections.Generic;

namespace SRChat
{
    [Serializable]
    public class ChatMessage
    {
        public ChatMessage()
        {
        }

        public string chatMessageId { get; set; }
        public string conversationId { get; set; }
        public string senderId { get; set; }
        public string senderName { get; set; }
        public string messageText { get; set; }
        public string displayPrefix { get { return string.Format("[{0}] {1}:", timestamp.ToShortTimeString(), senderName); } }
        public DateTime timestamp { get; set; }
    }
}