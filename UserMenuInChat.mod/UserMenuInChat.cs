using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

using ScrollsModLoader.Interfaces;
using UnityEngine;
using Mono.Cecil;

namespace UserMenuInChat.mod {
    public class UserMenuInChat : BaseMod {
        private const bool debug = false;

        private ChatUI target = null;
        private ChatRooms chatRooms;
        private GUIStyle timeStampStyle;
        private GUIStyle chatLogStyle;
        private MethodInfo createUserMenu;
        private Regex userRegex;
        // dict from room log, to another dict that maps chatline to a username
        private Dictionary<ChatRooms.RoomLog, Dictionary<ChatRooms.ChatLine, string>> chatLineToUserNameCache = new Dictionary<ChatRooms.RoomLog, Dictionary<ChatRooms.ChatLine, string>>();
        // dict from room name, to another dict that maps username to ChatUser
        private Dictionary<string, Dictionary<string, ChatRooms.ChatUser>> userNameToUserCache = new Dictionary<string, Dictionary<string, ChatRooms.ChatUser>>();

        public UserMenuInChat() {
            // match until first instance of ':' (finds the username)
            userRegex = new Regex(@"[^:]*"
                /*, RegexOptions.Compiled*/); // the version of Mono used by Scrolls version of Unity does not support compiled regexes
        }

        public static string GetName() {
            return "UserMenuInChat";
        }

        public static int GetVersion() {
            return 3;
        }

        public static MethodDefinition[] GetHooks(TypeDefinitionCollection scrollsTypes, int version) {
            try {
                return new MethodDefinition[] {
                    scrollsTypes["ChatRooms"].Methods.GetMethod("LeaveRoom", new Type[]{typeof(string)}),
                    scrollsTypes["ChatRooms"].Methods.GetMethod("SetRoomInfo", new Type[] {typeof(RoomInfoMessage)}),
                    scrollsTypes["ChatUI"].Methods.GetMethod("OnGUI")[0]
                };
            }
            catch {
                return new MethodDefinition[] { };
            }
        }

        public override bool BeforeInvoke(InvocationInfo info, out object returnValue) {
            if (info.target is ChatRooms && info.targetMethod.Equals("LeaveRoom")) {
                string room = (string)info.arguments[0];
                if (userNameToUserCache.ContainsKey(room)) {
                    userNameToUserCache.Remove(room);
                }
            }

            returnValue = null;
            return false;
        }

        public override void AfterInvoke(InvocationInfo info, ref object returnValue) {
            if (info.target is ChatRooms && info.targetMethod.Equals("SetRoomInfo")) {
                RoomInfoMessage roomInfo = (RoomInfoMessage) info.arguments[0];
                if (!userNameToUserCache.ContainsKey(roomInfo.roomName)) {
                    userNameToUserCache.Add(roomInfo.roomName, new Dictionary<string, ChatRooms.ChatUser>());
                }
                Dictionary<string, ChatRooms.ChatUser> userCache = userNameToUserCache[roomInfo.roomName];
                userCache.Clear();

                RoomInfoMessage.RoomInfoProfile[] profiles = roomInfo.profiles;
                for (int i = 0; i < profiles.Length; i++) {
                    RoomInfoMessage.RoomInfoProfile p = profiles[i];
                    ChatRooms.ChatUser user = ChatRooms.ChatUser.fromRoomInfoProfile(p);
                    userCache.Add(user.name, user);
                }
            }
            else if (info.target is ChatUI && info.targetMethod.Equals("OnGUI")) {
                if (target != (ChatUI)info.target) {
                    chatRooms = (ChatRooms)typeof(ChatUI).GetField("chatRooms", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(info.target);
                    timeStampStyle = (GUIStyle)typeof(ChatUI).GetField("timeStampStyle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(info.target);
                    chatLogStyle = (GUIStyle)typeof(ChatUI).GetField("chatLogStyle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(info.target);
                    createUserMenu = typeof(ChatUI).GetMethod("CreateUserMenu", BindingFlags.Instance | BindingFlags.NonPublic);
                    target = (ChatUI)info.target;
                }
                ChatRooms.RoomLog currentRoomChatLog = chatRooms.GetCurrentRoomChatLog();
                if (currentRoomChatLog != null) {
                    // these need to be refetched on every run, because otherwise old values will be used
                    Rect chatlogAreaInner = (Rect)typeof(ChatUI).GetField("chatlogAreaInner", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(info.target);
                    Vector2 chatScroll = (Vector2)typeof(ChatUI).GetField("chatScroll", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(info.target);
                    bool allowSendingChallenges = (bool)typeof(ChatUI).GetField("allowSendingChallenges", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(info.target);
                    ContextMenu<ChatRooms.ChatUser> userContextMenu = (ContextMenu<ChatRooms.ChatUser>)typeof(ChatUI).GetField("userContextMenu", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(info.target);

                    // set invisible draw color. We want the layout effects of drawing stuff, but we let the
                    // original code do all of the actual drawing
                    Color oldColor = GUI.color;
                    // disable warning that one of these expressions is unreachable (due to debug being const)
                    #pragma warning disable 0429
                    GUI.color = debug ? Color.red : Color.clear;
                    #pragma warning restore 0429

                    GUILayout.BeginArea(chatlogAreaInner);
                    GUILayout.BeginScrollView(chatScroll, new GUILayoutOption[] { GUILayout.Width(chatlogAreaInner.width), GUILayout.Height(chatlogAreaInner.height)});
                    foreach (ChatRooms.ChatLine current in currentRoomChatLog.getLines()) {
                        GUILayout.BeginHorizontal(new GUILayoutOption[0]);
                        GUILayout.Label(current.timestamp, timeStampStyle, new GUILayoutOption[] {
                            GUILayout.Width(20f + (float)Screen.height * 0.042f)});

                        if (!chatLineToUserNameCache.ContainsKey(currentRoomChatLog)) {
                            chatLineToUserNameCache.Add(currentRoomChatLog, new Dictionary<ChatRooms.ChatLine, string>());
                        }
                        Dictionary<ChatRooms.ChatLine, string> userCache = chatLineToUserNameCache[currentRoomChatLog];
                        if (!userCache.ContainsKey(current)) {
                            Match userMatch = userRegex.Match(current.text);
                            if (userMatch.Success) {
                                // strip HTML from user name (usually a color). Yes. I know. Regexes should not be used on 
                                // XML, but here it should not pose a problem
                                String strippedMatch = Regex.Replace(userMatch.Value, @"<[^>]*>", String.Empty);
                                userCache.Add(current, strippedMatch);
                            }
                        }
                        string sender;
                        bool foundSender = userCache.TryGetValue(current, out sender);
                        bool foundUser = false;
                        Dictionary<string, ChatRooms.ChatUser> roomUsers;
                        bool foundRoomUsers = userNameToUserCache.TryGetValue(chatRooms.GetCurrentRoom(), out roomUsers);
                        if (foundSender && foundRoomUsers) {
                            ChatRooms.ChatUser user;
                            foundUser = roomUsers.TryGetValue(sender, out user);
                            if (foundUser) {
                                if (GUILayout.Button(current.text, chatLogStyle, new GUILayoutOption[] { GUILayout.Width(chatlogAreaInner.width - (float)Screen.height * 0.1f - 20f) }) &&
                                    !(App.MyProfile.ProfileInfo.id == user.id) && allowSendingChallenges && userContextMenu == null) {
                                    createUserMenu.Invoke(info.target, new object[] { user });
                                    App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");
                                }
                            }
                        }
                        // do the drawing found in the original code to make sure we don't fall out of sync
                        if (!foundUser || !foundSender || !foundRoomUsers) {
                            GUILayout.Label(current.text, chatLogStyle, new GUILayoutOption[] {
				                        GUILayout.Width(chatlogAreaInner.width - (float)Screen.height * 0.1f - 20f)});
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndScrollView();
                    GUILayout.EndArea();
                    // restore old color. Should not be necessary, but it does not hurt to be paranoid
                    GUI.color = oldColor;
                }
            }
            return;
        }
    }
}
