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
        private bool debug = false;
        private ChatUI target = null;
        private ChatRooms chatRooms;
        private GUIStyle timeStampStyle;
        private GUIStyle chatLogStyle;
        private MethodInfo createUserMenu;

        public UserMenuInChat() {
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
                    scrollsTypes["ChatUI"].Methods.GetMethod("OnGUI")[0]
                };
            }
            catch {
                return new MethodDefinition[] { };
            }
        }


        public override bool BeforeInvoke(InvocationInfo info, out object returnValue) {
            returnValue = null;
            return false;
        }

        public override void AfterInvoke(InvocationInfo info, ref object returnValue) {
            if (info.target is ChatUI && info.targetMethod.Equals("OnGUI")) {
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

                    // set invisible draw color. We want the layout effects of drawing stuff, but we let the 
                    // original code do all of the actual drawing
                    Color oldColor = GUI.color;
                    GUI.color = debug ? Color.red : Color.clear;
                    GUILayout.BeginArea(chatlogAreaInner);
                    GUILayout.BeginScrollView(chatScroll, new GUILayoutOption[] { GUILayout.Width(chatlogAreaInner.width), GUILayout.Height(chatlogAreaInner.height)});
                    foreach (ChatRooms.ChatLine current in currentRoomChatLog.getLines()) {
                        GUILayout.BeginHorizontal(new GUILayoutOption[0]);
                        GUILayout.Label(current.timestamp, timeStampStyle, new GUILayoutOption[] {
                            GUILayout.Width(20f + (float)Screen.height * 0.042f)});
                        // match until first instance of ':' (find username)
                        string userRegexStr = @"[^:]*";
                        Regex userRegex = new Regex(userRegexStr);
                        Match userMatch = userRegex.Match(current.text);
                        if (userMatch.Success) {
                            List<ChatRooms.ChatUser> currentRoomUsers = chatRooms.GetCurrentRoomUsers();
                            // strip HTML from results. Yes. I know. Regexes should not be used on XML, but here it
                            // should not pose a problem
                            String strippedMatch = Regex.Replace(userMatch.Value, @"<[^>]*>", String.Empty);
                            bool foundUser = false;
                            foreach (ChatRooms.ChatUser user in currentRoomUsers) {
                                if (strippedMatch.Equals(user.name)) {
                                    bool allowSendingChallenges = (bool)typeof(ChatUI).GetField("allowSendingChallenges", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(info.target);
                                    ContextMenu<ChatRooms.ChatUser> userContextMenu = (ContextMenu<ChatRooms.ChatUser>)typeof(ChatUI).GetField("userContextMenu", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(info.target);
                                    foundUser = true;
                                    if (GUILayout.Button(current.text, chatLogStyle, new GUILayoutOption[] { GUILayout.Width(chatlogAreaInner.width - (float)Screen.height * 0.1f - 20f) }) &&
                                        !(App.MyProfile.ProfileInfo.id == user.id) && allowSendingChallenges && userContextMenu == null) {
                                        createUserMenu.Invoke(info.target, new object[] { user });
                                        App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");
                                    }
                                }
                            }
                            // do the drawing found in the original code to make sure we don't fall out of sync
                            if (!foundUser) {
                                GUILayout.Label(current.text, chatLogStyle, new GUILayoutOption[] {
				                        GUILayout.Width(chatlogAreaInner.width - (float)Screen.height * 0.1f - 20f)});
                            }
                        }
                        // restore old color. Should not be necessary, but it does not hurt to be paranoid
                        GUI.color = oldColor;
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndScrollView();
                    GUILayout.EndArea();
                }
            }
            return;
        }
    }
}
