using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using SignalR.Hubs;

namespace SRChat
{
    [HubName("sRChatServer")]
    public class SRChatServer : Hub
    {
        #region Private Variables
        private static readonly ConcurrentDictionary<string, MessageRecipient> _chatUsers = new ConcurrentDictionary<string, MessageRecipient>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, ChatRoom> _chatRooms = new ConcurrentDictionary<string, ChatRoom>(StringComparer.OrdinalIgnoreCase);
        #endregion

        #region Public Methods

        public bool Connect(string userId, string userName)
        {
            try
            {
                if (string.IsNullOrEmpty(userId) | string.IsNullOrEmpty(userName))
                {
                    return false;
                }
                if (GetChatUserByUserId(userId) == null)
                {
                    AddUser(userId, userName);
                }
                else
                {
                    ModifyUser(userId, userName);
                }
                SendOnlineContacts();
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Problem in connecting to chat server!");
            }
        }
        public override Task Disconnect()
        {
            try
            {
                DeleteUser(Context.ConnectionId);
                return null;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Problem in disconnecting from chat server!");
            }
        }
        public bool InitiateChat(string fromUserId, string fromUserName, string toUserId, string toUserName)
        {
            try
            {
                if (string.IsNullOrEmpty(fromUserId) || string.IsNullOrEmpty(fromUserName) || string.IsNullOrEmpty(toUserId) || string.IsNullOrEmpty(toUserName))
                {
                    return false;
                }

                var fromUser = GetChatUserByUserId(fromUserId);
                var toUser = GetChatUserByUserId(toUserId);

                if (fromUser != null && toUser != null)
                {
                    if (!CheckIfRoomExists(fromUser, toUser))
                    {
                        //Create New Chat Room
                        ChatRoom chatRoom = new ChatRoom();
                        chatRoom.chatRoomInitiatedBy = fromUser.messageRecipientId;
                        chatRoom.chatRoomInitiatedTo = toUser.messageRecipientId;

                        chatRoom.messageRecipients.Add(fromUser);
                        chatRoom.messageRecipients.Add(toUser);

                        //create and save blank message to get new conversation id
                        ChatMessage chatMessage = new ChatMessage();
                        chatMessage.messageText = "Chat Initiated";
                        chatMessage.senderId = fromUser.messageRecipientId;
                        chatMessage.senderName = fromUser.messageRecipientName;

                        fromUser.chatRoomIds.Add(chatRoom.chatRoomId);
                        toUser.chatRoomIds.Add(chatRoom.chatRoomId);

                        //Create SignalR Group for this chat room and add users connection to it
                        Groups.Add(fromUser.connectionId, chatRoom.chatRoomId);
                        Groups.Add(toUser.connectionId, chatRoom.chatRoomId);

                        //Add Chat room object to collection
                        if (_chatRooms.TryAdd(chatRoom.chatRoomId, chatRoom))
                        {
                            //Generate Client UI for this room
                            Clients[fromUser.connectionId].initiateChatUI(chatRoom);
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Problem in starting chat!");
            }
        }
        public bool EndChat(ChatMessage chatMessage)
        {
            try
            {
                ChatRoom chatRoom;
                if (_chatRooms.TryGetValue(chatMessage.conversationId, out chatRoom))
                {
                    if (_chatRooms[chatRoom.chatRoomId].chatRoomInitiatedBy == chatMessage.senderId)
                    {
                        chatMessage.messageText = string.Format("{0} left the chat. Chat Ended!", chatMessage.senderName);
                        if (_chatRooms.TryRemove(chatRoom.chatRoomId, out chatRoom))
                        {
                            Clients[chatRoom.chatRoomId].receiveEndChatMessage(chatMessage);
                            foreach (MessageRecipient messageReceipient in chatRoom.messageRecipients)
                            {
                                if (messageReceipient.chatRoomIds.Contains(chatRoom.chatRoomId))
                                {
                                    messageReceipient.chatRoomIds.Remove(chatRoom.chatRoomId);
                                    Groups.Remove(messageReceipient.connectionId, chatRoom.chatRoomId);
                                }
                            }
                        }
                    }
                    else
                    {
                        MessageRecipient messageRecipient = GetChatUserByUserId(chatMessage.senderId);
                        if (messageRecipient != null && messageRecipient.chatRoomIds.Contains(chatRoom.chatRoomId))
                        {
                            chatRoom.messageRecipients.Remove(messageRecipient);
                            messageRecipient.chatRoomIds.Remove(chatRoom.chatRoomId);
                            if (chatRoom.messageRecipients.Count < 2)
                            {
                                chatMessage.messageText = string.Format("{0} left the chat. Chat Ended!", chatMessage.senderName);
                                if (_chatRooms.TryRemove(chatRoom.chatRoomId, out chatRoom))
                                {
                                    Clients[chatRoom.chatRoomId].receiveEndChatMessage(chatMessage);
                                    foreach (MessageRecipient messageReceipient in chatRoom.messageRecipients)
                                    {
                                        if (messageReceipient.chatRoomIds.Contains(chatRoom.chatRoomId))
                                        {
                                            messageReceipient.chatRoomIds.Remove(chatRoom.chatRoomId);
                                            Groups.Remove(messageReceipient.connectionId, chatRoom.chatRoomId);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                chatMessage.messageText = string.Format("{0} left the chat.", chatMessage.senderName);
                                Groups.Remove(messageRecipient.connectionId, chatRoom.chatRoomId);
                                Clients[messageRecipient.connectionId].receiveEndChatMessage(chatMessage);
                                Clients[chatRoom.chatRoomId].receiveLeftChatMessage(chatMessage);
                                Clients[chatRoom.chatRoomId].updateChatUI(chatRoom);
                            }
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException("Problem in ending chat!");
                }
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Problem in ending chat!");
            }
        }
        public bool SendChatMessage(ChatMessage chatMessage)
        {
            try
            {
                ChatRoom chatRoom;
                if (_chatRooms.TryGetValue(chatMessage.conversationId, out chatRoom))
                {
                    chatMessage.chatMessageId = Guid.NewGuid().ToString();
                    chatMessage.timestamp = DateTime.Now;
                    Clients[chatMessage.conversationId].receiveChatMessage(chatMessage, chatRoom);
                    return true;
                }
                else
                {
                    throw new InvalidOperationException("Problem in sending message!");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Problem in sending message!");
            }
        }
        private bool SendOnlineContacts()
        {
            try
            {
                OnlineContacts onlineContacts = new OnlineContacts();
                foreach (var item in _chatUsers)
                {
                    onlineContacts.messageRecipients.Add(item.Value);
                }
                Clients.onGetOnlineContacts(onlineContacts);
                return false;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Problem in getting contacts!");
            }
        }

        #endregion

        #region Private Methods

        private Boolean CheckIfRoomExists(MessageRecipient fromUser, MessageRecipient toUser)
        {
            foreach (string chatRoomId in fromUser.chatRoomIds)
            {
                Int32 count = (from mr in _chatRooms[chatRoomId].messageRecipients
                               where mr.messageRecipientId == toUser.messageRecipientId
                               select mr).Count();
                if (count > 0)
                {
                    return true;
                }
            }
            foreach (string chatRoomId in toUser.chatRoomIds)
            {
                Int32 count = (from mr in _chatRooms[chatRoomId].messageRecipients
                               where mr.messageRecipientId == fromUser.messageRecipientId
                               select mr).Count();
                if (count > 0)
                {
                    return true;
                }
            }
            return false;
        }
        private MessageRecipient AddUser(string userId, string userName)
        {
            var user = new MessageRecipient();
            user.messageRecipientId = userId;
            user.messageRecipientName = userName;
            user.connectionId = Context.ConnectionId;
            _chatUsers[userId] = user;
            return user;
        }
        private MessageRecipient ModifyUser(string userId, string userName)
        {
            var user = GetChatUserByUserId(userId);
            user.messageRecipientName = userName;
            user.connectionId = Context.ConnectionId;
            _chatUsers[userId] = user;
            return user;
        }
        private Boolean DeleteUser(string userId, string userName)
        {
            var user = GetChatUserByUserId(userId);
            if (user != null && _chatUsers.ContainsKey(user.messageRecipientId))
            {
                MessageRecipient messageRecipient;
                return _chatUsers.TryRemove(user.messageRecipientId, out messageRecipient);
            }
            return false;
        }
        private Boolean DeleteUser(string connectionId)
        {
            var returnValue = false;
            var user = GetChatUserByConnectionId(connectionId);
            if (user != null && _chatUsers.ContainsKey(user.messageRecipientId))
            {
                MessageRecipient messageRecipient;
                returnValue = _chatUsers.TryRemove(user.messageRecipientId, out messageRecipient);

                //remoave from all groups and chatrooms
                foreach (string chatRoomId in messageRecipient.chatRoomIds)
                {
                    _chatRooms[chatRoomId].messageRecipients.Remove(messageRecipient);

                    Groups.Remove(messageRecipient.connectionId, chatRoomId);

                    //notify user left chat
                    ChatMessage chatMessage = new ChatMessage();
                    chatMessage.conversationId = chatRoomId;
                    chatMessage.senderId = messageRecipient.messageRecipientId;
                    chatMessage.senderName = messageRecipient.messageRecipientName;
                    if (_chatRooms[chatRoomId].chatRoomInitiatedBy == messageRecipient.messageRecipientId)
                    {
                        chatMessage.messageText = string.Format("{0} left the chat. Chat Ended!", messageRecipient.messageRecipientName);
                        ChatRoom chatRoom;

                        if (_chatRooms.TryRemove(chatRoomId, out chatRoom))
                        {
                            foreach (MessageRecipient messageReceipient in chatRoom.messageRecipients)
                            {
                                if (messageReceipient.chatRoomIds.Contains(chatRoomId))
                                {
                                    messageReceipient.chatRoomIds.Remove(chatRoomId);
                                }
                            }
                            Clients[chatRoomId].receiveEndChatMessage(chatMessage);
                        }
                    }
                    else
                    {
                        if (_chatRooms[chatRoomId].messageRecipients.Count() < 2)
                        {
                            chatMessage.messageText = string.Format("{0} left the chat. Chat Ended!", messageRecipient.messageRecipientName);
                            ChatRoom chatRoom;
                            if (_chatRooms.TryRemove(chatRoomId, out chatRoom))
                            {
                                foreach (MessageRecipient messageReceipient in chatRoom.messageRecipients)
                                {
                                    if (messageReceipient.chatRoomIds.Contains(chatRoomId))
                                    {
                                        messageReceipient.chatRoomIds.Remove(chatRoomId);
                                    }
                                }
                                Clients[chatRoomId].receiveEndChatMessage(chatMessage);
                            }
                        }
                        else
                        {
                            chatMessage.messageText = string.Format("{0} left the chat.", messageRecipient.messageRecipientName);
                            Clients[chatRoomId].receiveLeftChatMessage(chatMessage);
                        }
                    }
                }
            }
            return returnValue;
        }
        private MessageRecipient GetChatUserByUserId(string userId)
        {
            return _chatUsers.Values.FirstOrDefault(u => u.messageRecipientId == userId);
        }
        private MessageRecipient GetChatUserByConnectionId(string connectionId)
        {
            return _chatUsers.Values.FirstOrDefault(u => u.connectionId == connectionId);
        }

        #endregion
    }
}