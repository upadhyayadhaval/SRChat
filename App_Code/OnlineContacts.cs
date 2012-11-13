using System;
using System.Collections.Generic;

namespace SRChat
{
    [Serializable]
    public class OnlineContacts
    {
        public List<MessageRecipient> messageRecipients { get; set; }
        public OnlineContacts()
        {
            messageRecipients = new List<MessageRecipient>();
        }
    }
}