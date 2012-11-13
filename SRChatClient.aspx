<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="SRChatClient.aspx.cs" Inherits="SRChat.SRChatClient" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title></title>
    <style>
        .chatRooms
        {
            max-height: 500px;
            overflow: auto;
        }
        .chatRoom
        {
            width: 100%;
            height: 250px;
            border: 1px solid #ccc;
        }
        .chatMessages
        {
            width: 100%;
            height: 200px;
            overflow: auto;
            margin-left: 0px;
            padding-left: 0px;
        }
        .chatMessages li
        {
            list-style-type: none;
            padding: 1px;
        }
        .chatNewMessage
        {
            border: 1px solid #ccc;
            width: 200px;
            float: left;
            height: 18px;
        }
        .chatMessage
        {
        }
        .chatSend
        {
            float: left;
        }
    </style>
    <link rel="stylesheet" href="Styles/jquery-ui.css" />
    <script src="Scripts/jquery-1.8.2.js"></script>
    <script src="Scripts/jquery-ui.js"></script>
    <script type="text/javascript" src="Scripts/jquery.dialogextend.1_0_1.js"></script>
    <script type="text/javascript" src="Scripts/jquery.signalR.js"></script>
    <script type="text/javascript" src="Scripts/jQuery.tmpl.js"></script>
    <script type="text/javascript" src="signalr/hubs"></script>
    <script id="new-online-contacts" type="text/x-jquery-tmpl">
        <div>
        <ul>
        {{each messageRecipients}}
            <li id="chatLink${messageRecipientId}"><a href="javascript:;" onclick="javascript:SRChat.initiateChat('${messageRecipientId}','${messageRecipientName}');">${messageRecipientName}</a></li>
        {{/each}}
        </ul>
        </div>
    </script>
    <script id="new-chatroom-template" type="text/x-jquery-tmpl">
    <div id="chatRoom${chatRoomId}" class="chatRoom">
        <ul id="messages${chatRoomId}" class="chatMessages">
        </ul>
        <form id="sendmessage${chatRoomId}" action="#">
            <input type="text" id="newmessage${chatRoomId}" class="chatNewMessage"/>
            <div class="clear"></div>
            <input type="button" id="chatsend${chatRoomId}" value="Send" class="chatSend" onClick="javascript:SRChat.sendChatMessage('${chatRoomId}')" />
            <input type="button" id="chatend${chatRoomId}" value="End Chat" class="chatSend" onClick="javascript:SRChat.endChat('${chatRoomId}')" />
        </form>
    </div>
    </script>
    <script id="new-chat-header" type="text/x-jquery-tmpl">
    <div id="chatRoomHeader${chatRoomId}">
        {{each messageRecipients}}
            {{if $index == 0}}
                ${messageRecipientName}
            {{else}}
                , ${messageRecipientName}
            {{/if}}
        {{/each}}
    <div>
    </script>
    <script id="new-message-template" type="text/x-jquery-tmpl">
    <li class="message" id="m-${chatMessageId}">
        <strong>${displayPrefix}</strong>
        {{html messageText}}
    </li>
    </script>
    <script id="new-notify-message-template" type="text/x-jquery-tmpl">
    <li class="message" id="m-${chatMessageId}">
        <strong>{{html messageText}}</strong>
    </li>
    </script>
    <script type="text/javascript"> 
    //<![CDATA[

        $(document).ready(function () {
            SRChat.attachEvents();
        });



        SRChat = new function () {
            var chatRooms = 0;
            var numRand = Math.floor(Math.random() * 1000)
            var senderId = numRand;
            var senderName = 'User ' + numRand;

            var sRChatServer;

            window.onbeforeunload = function () {
                if (chatRooms > 0)
                    return "All chat instances will be ended!";
            };

            this.attachEvents = function () {
                $("#userNameLabel").html(senderName);
                if ($.connection != null) {
                    jQuery.support.cors = true;
                    $.connection.hub.url = 'signalr/hubs';
                    sRChatServer = $.connection.sRChatServer;

                    $.connection.hub.start({ transport: 'auto' }, function () {
                        sRChatServer.server.connect(senderId, senderName).fail(function (e) {
                            alert(e);
                        });
                    });

                    sRChatServer.client.initiateChatUI = function (chatRoom) {
                        var chatRoomDiv = $('#chatRoom' + chatRoom.chatRoomId);
                        if (($(chatRoomDiv).length > 0)) {
                            var chatRoomText = $('#newmessage' + chatRoom.chatRoomId);
                            var chatRoomSend = $('#chatsend' + chatRoom.chatRoomId);
                            var chatRoomEndChat = $('#chatend' + chatRoom.chatRoomId);

                            chatRoomText.show();
                            chatRoomSend.show();
                            chatRoomEndChat.show();
                        }
                        else {
                            var e = $('#new-chatroom-template').tmpl(chatRoom);
                            var c = $('#new-chat-header').tmpl(chatRoom);

                            chatRooms++;

                            //dialog options
                            var dialogOptions = {
                                "id": '#messages' + chatRoom.chatRoomId,
                                "title": c,
                                "width": 360,
                                "height": 365,
                                "modal": false,
                                "resizable": false,
                                "close": function () { javascript: SRChat.endChat('' + chatRoom.chatRoomId + ''); $(this).remove(); }
                            };

                            // dialog-extend options
                            var dialogExtendOptions = {
                                "close": true,
                                "maximize": false,
                                "minimize": true,
                                "dblclick": 'minimize',
                                "titlebar": 'transparent'
                            };

                            e.dialog(dialogOptions).dialogExtend(dialogExtendOptions);

                            $('#sendmessage' + chatRoom.chatRoomId).keypress(function (e) {
                                if ((e.which && e.which == 13) || (e.keyCode && e.keyCode == 13)) {
                                    $('#chatsend' + chatRoom.chatRoomId).click();
                                    return false;
                                }
                            });
                        }
                    };

                    sRChatServer.client.updateChatUI = function (chatRoom) {
                        var chatRoomHeader = $('#chatRoomHeader' + chatRoom.chatRoomId);
                        var c = $('#new-chat-header').tmpl(chatRoom);
                        chatRoomHeader.html(c);
                    };

                    sRChatServer.client.receiveChatMessage = function (chatMessage, chatRoom) {
                        sRChatServer.client.initiateChatUI(chatRoom);
                        var chatRoom = $('#chatRoom' + chatMessage.conversationId);
                        var chatRoomMessages = $('#messages' + chatMessage.conversationId);
                        var e = $('#new-message-template').tmpl(chatMessage).appendTo(chatRoomMessages);
                        e[0].scrollIntoView();
                        chatRoom.scrollIntoView();
                    };

                    sRChatServer.client.receiveLeftChatMessage = function (chatMessage) {
                        var chatRoom = $('#chatRoom' + chatMessage.conversationId);
                        var chatRoomMessages = $('#messages' + chatMessage.conversationId);
                        var e = $('#new-notify-message-template').tmpl(chatMessage).appendTo(chatRoomMessages);
                        e[0].scrollIntoView();
                        chatRoom.scrollIntoView();
                    };

                    sRChatServer.client.receiveEndChatMessage = function (chatMessage) {
                        var chatRoom = $('#chatRoom' + chatMessage.conversationId);
                        var chatRoomMessages = $('#messages' + chatMessage.conversationId);
                        var chatRoomText = $('#newmessage' + chatMessage.conversationId);
                        var chatRoomSend = $('#chatsend' + chatMessage.conversationId);
                        var chatRoomEndChat = $('#chatend' + chatMessage.conversationId);

                        chatRooms--;

                        var e = $('#new-notify-message-template').tmpl(chatMessage).appendTo(chatRoomMessages);

                        chatRoomText.hide();
                        chatRoomSend.hide();
                        chatRoomEndChat.hide();

                        e[0].scrollIntoView();
                        chatRoom.scrollIntoView();
                    };

                    sRChatServer.client.onGetOnlineContacts = function (chatUsers) {
                        var e = $('#new-online-contacts').tmpl(chatUsers);
                        var chatLink = $('#chatLink' + senderId);
                        e.find("#chatLink" + senderId).remove();
                        $("#chatOnlineContacts").html("");
                        $("#chatOnlineContacts").html(e);
                    };
                }
            };

            this.sendChatMessage = function (chatRoomId) {
                var chatRoomNewMessage = $('#newmessage' + chatRoomId);

                if (chatRoomNewMessage.val() == null || chatRoomNewMessage.val() == "")
                    return;

                var chatMessage = {
                    senderId: senderId,
                    senderName: senderName,
                    conversationId: chatRoomId,
                    messageText: chatRoomNewMessage.val()
                };

                chatRoomNewMessage.val('');
                chatRoomNewMessage.focus();
                sRChatServer.server.sendChatMessage(chatMessage).fail(function (e) {
                    alert(e);
                });

                return false;
            };

            this.endChat = function (chatRoomId) {
                var chatRoomNewMessage = $('#newmessage' + chatRoomId);

                var chatMessage = {
                    senderId: senderId,
                    senderName: senderName,
                    conversationId: chatRoomId,
                    messageText: chatRoomNewMessage.val()
                };
                chatRoomNewMessage.val('');
                chatRoomNewMessage.focus();
                sRChatServer.server.endChat(chatMessage).fail(function (e) {
                    //alert(e);
                });
            };

            this.initiateChat = function (toUserId, toUserName) {
                if (sRChatServer == null) {
                    alert("Problem in connecting to Chat Server. Please Contact Administrator!");
                    return;
                }
                sRChatServer.server.initiateChat(senderId, senderName, toUserId, toUserName).fail(function (e) {
                    alert(e);
                });
            };

        };
    //]]> 
    </script>
</head>
<body>
    <form id="form1" runat="server">
    <h3>
        SRChat - By Dhaval Upadhyaya - <a href="http://dhavalupadhyaya.wordpress.com/about-me/"
            target="_blank">http://dhavalupadhyaya.wordpress.com/about-me/</a>
    </h3>
    <div>
        <div id="userNameLabel">
        </div>
        <br />
        <br />
        <div id="chatRooms">
        </div>
        <div id="chatOnlineContacts">
        </div>
    </div>
    </form>
</body>
</html>
