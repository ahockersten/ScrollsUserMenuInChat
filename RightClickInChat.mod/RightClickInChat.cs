using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

using ScrollsModLoader.Interfaces;
using UnityEngine;
using Mono.Cecil;

using System.Collections;

// FIXME check existence of methods, private variables etc and fail gracefully!

namespace RightClickInChat.mod {
    public class RightClickInChat : BaseMod {

        //initialize everything here, Game is loaded at this point
        public RightClickInChat() {
        }


        public static string GetName() {
            return "RightClickInChat";
        }

        public static int GetVersion() {
            return 1;
        }

        //only return MethodDefinitions you obtained through the scrollsTypes object
        //safety first! surround with try/catch and return an empty array in case it fails
        public static MethodDefinition[] GetHooks(TypeDefinitionCollection scrollsTypes, int version) {
            try {
                return new MethodDefinition[] {
                    scrollsTypes["ChatUI"].Methods.GetMethod("OnGUI")[0]
                };
            }
            catch {
                Console.WriteLine("RightClickInChat failed to connect to methods used.");
                return new MethodDefinition[] { };
            }
        }


        public override bool BeforeInvoke(InvocationInfo info, out object returnValue) {
            returnValue = null;
            return false;
        }

        public override void AfterInvoke(InvocationInfo info, ref object returnValue) {
            if (info.target is ChatUI && info.targetMethod.Equals("OnGUI")) {
                ChatRooms chatRooms = (ChatRooms)typeof(ChatUI).GetField("chatRooms", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(info.target);
                ChatRooms.RoomLog currentRoomChatLog = chatRooms.GetCurrentRoomChatLog();
                if (currentRoomChatLog != null) {
                    Rect chatlogAreaInner = (Rect)typeof(ChatUI).GetField("chatlogAreaInner", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(info.target);
                    Vector2 chatScroll = (Vector2)typeof(ChatUI).GetField("chatScroll", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(info.target);
                    float maxScroll = (float)typeof(ChatUI).GetField("maxScroll", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(info.target);
                    GUIStyle timeStampStyle = (GUIStyle)typeof(ChatUI).GetField("timeStampStyle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(info.target);
                    GUIStyle chatLogStyle = (GUIStyle)typeof(ChatUI).GetField("chatLogStyle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(info.target);
                    bool allowSendingChallenges = (bool)typeof(ChatUI).GetField("allowSendingChallenges", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(info.target);
                    ContextMenu<ChatRooms.ChatUser> userContextMenu = (ContextMenu<ChatRooms.ChatUser>)typeof(ChatUI).GetField("userContextMenu", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(info.target);
                    MethodInfo createUserMenu = typeof(ChatUI).GetMethod("CreateUserMenu", BindingFlags.Instance | BindingFlags.NonPublic);
                    
                    GUI.color = new Color(1f, 1f, 1f, 0.5f);
                    GUI.Box(new Rect(chatlogAreaInner.xMax - 15f, chatlogAreaInner.y, 15f, chatlogAreaInner.height), string.Empty);
                    GUI.color = Color.white;
                    GUILayout.BeginArea(chatlogAreaInner);
                    chatScroll = GUILayout.BeginScrollView(chatScroll, new GUILayoutOption[] {
                        GUILayout.Width(chatlogAreaInner.width),
                        GUILayout.Height(chatlogAreaInner.height)
                    });
                    if (chatScroll.y != float.PositiveInfinity) {
                        maxScroll = Mathf.Max(maxScroll, chatScroll.y);
                    }
                    foreach (ChatRooms.ChatLine current in currentRoomChatLog.getLines()) {
                        AdminRole senderAdminRole = (AdminRole)typeof(ChatRooms.ChatLine).GetField("senderAdminRole", BindingFlags.Instance | BindingFlags.Public).GetValue(current);
                        GUILayout.BeginHorizontal(new GUILayoutOption[0]);
                        GUILayout.Label(current.timestamp, timeStampStyle, new GUILayoutOption[] {
                            GUILayout.Width(20f + (float)Screen.height * 0.042f)
                        });
                        GUI.color = new Color(1f, 1f, 1f, 0.65f);
                        if (senderAdminRole == AdminRole.Mojang) {
                            GUILayout.Label(ResourceManager.LoadTexture("ChatUI/mojang_icon"), new GUILayoutOption[] {
	                            GUILayout.Width((float)chatLogStyle.fontSize),
	                            GUILayout.Height((float)chatLogStyle.fontSize)
                            });
                        }
                        else {
                            if (senderAdminRole == AdminRole.Moderator) {
                                GUILayout.Label(ResourceManager.LoadTexture("ChatUI/moderator_icon"), new GUILayoutOption[] {
           		                    GUILayout.Width((float)chatLogStyle.fontSize),
		                            GUILayout.Height((float)chatLogStyle.fontSize)
	                            });
                            }
                        }
                        string userRegexStr = @"[^:]*"; // find first instance of ':'
                        Regex userRegex = new Regex(userRegexStr);
                        Match userMatch = userRegex.Match(current.text);
                        if (userMatch.Success) {
                            List<ChatRooms.ChatUser> currentRoomUsers = chatRooms.GetCurrentRoomUsers();
                            // strip HTML from results. Yes. I know. Regexex should not be used on XML, but here it
                            // should not pose a problem
                            String strippedMatch = Regex.Replace(userMatch.Value, @"<[^>]*>", String.Empty);
                            foreach (ChatRooms.ChatUser user in currentRoomUsers) {
                                if (strippedMatch.Equals(user.name)) {
                                    if (GUILayout.Button(userMatch.Value, new GUILayoutOption[] { GUILayout.Width(chatlogAreaInner.width - (float)Screen.height * 0.1f - 20f) }) &&
                                        !(App.MyProfile.ProfileInfo.id == user.id) && allowSendingChallenges && userContextMenu == null) {
                                            createUserMenu.Invoke(info.target, new object[] { user });
                                            App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");
                                    }
                                    GUI.color = Color.red;
                                    GUILayout.Label(":" + userMatch.NextMatch().Value, new GUILayoutOption[] {
                                        GUILayout.Width(chatlogAreaInner.width - (float)Screen.height * 0.1f - 20f)
                                    });
                                }
                            }
                        }
                        /*GUI.color = Color.white;
                        GUILayout.Label(current.text, chatLogStyle, new GUILayoutOption[] {
                            GUILayout.Width(chatlogAreaInner.width - (float)Screen.height * 0.1f - 20f)
                        });*/
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
