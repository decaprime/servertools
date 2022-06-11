using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Stunlock.Network;
using UnhollowerRuntimeLib;
using Wetstone.Hooks;
using Random = System.Random;

namespace servertools

{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.molenzwiebel.wetstone")]
    public class MainClass: BasePlugin
    {
        private const string PluginGuid = "neanka.servertools";
        private const string PluginName = "servertools";
        private const string PluginVersion = "3.0.0.0";
        public static ManualLogSource logger;
        private static Harmony harmony;

        public static ConfigFile conf;

        public static ConfigEntry<bool> configEnableDiscordBot;
        //private static ConfigEntry<ulong> configGuildID;
        public static ConfigEntry<ulong> configChannelID;
        public static ConfigEntry<string> configToken;
        private static ConfigEntry<string> configMOTD;
        private static ConfigEntry<bool> configMOTDEnabled;
        private static ConfigEntry<bool> configShowUserConnectedInDC;
        private static ConfigEntry<bool> configShowUserConnectedInGameChat;
        private static ConfigEntry<bool> configShowUserDisConnectedInDC;
        private static ConfigEntry<bool> configShowUserDisConnectedInGameChat;
        public static ConfigEntry<float> configAnnounceTimer;
        private static ConfigEntry<bool> configAnnounceEnabled;
        public static ConfigEntry<bool> configAnnounceRandomOrder;
        public static ConfigEntry<string> configIngameMessagePrefix;
        public static ConfigEntry<string> configCommandToReloadMessages;
        
        
        public static List<string> announce_entries;
        private static List<ConfigEntry<string>> configAnnounceMessages;
        
        
        public static int entries_count;
        public static int last_entry;
        public static float timer;
        public static bool rnd;
        public static Random random;
        public static bool announcesEnabled;
        public static string ingameMessagePrefix;
        public static string commandToReloadMessages;
        
        
        public override void Load()
        {
            logger = Log;
            conf = Config;
            logger.LogWarning("Hello, world!!!");
            configEnableDiscordBot = conf.Bind("General", "EnableDiscordBot", true, "Enable discord features");
            configChannelID = conf.Bind("General", "ChannelID", (ulong)0, "Channel ID for broadcast");
            configToken = conf.Bind("General", "Token", "YOUR_TOKEN", "Bot Token from https://discord.com/developers/applications");
            configIngameMessagePrefix = conf.Bind("General", "IngameMessagePrefix", "[DC]", "Prefix for ingame messages from discord");
            configCommandToReloadMessages = conf.Bind("General", "CommandToReloadMessages", "!rm", "Chat command which allow reload auto messages from config");
            ingameMessagePrefix = configIngameMessagePrefix.Value;
            commandToReloadMessages = configCommandToReloadMessages.Value;
            ReloadConf();
            Chat.OnChatMessage += HandleChatMessage;
            ClassInjector.RegisterTypeInIl2Cpp<Announcements>();
            AddComponent<Announcements>();
            logger.LogWarning("Patching");
            harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
            logger.LogWarning("Patching done");
        }
        
        public void HandleChatMessage(VChatEvent ev)
        {
            if (!ev.Cancelled)
            {
                if (ev.User.IsAdmin && ev.Message.Equals(commandToReloadMessages))
                {
                    ReloadConf();
                    ev.Cancel();
                }
                else if (ev.Type == ChatMessageType.Global)
                {
                    Announcements.Post($"{ev.User.CharacterName}: {ev.Message}").GetAwaiter();
                }
            }
        }

        public static void ReloadConf()
        {
            logger.LogWarning("conf.Reload();");
            conf.Reload();
            configMOTD = conf.Bind("Announcements", "MOTD", "Hello here is MOTD", "Set message of the day");
            configMOTDEnabled = conf.Bind("Announcements", "MOTDEnabled", false, "Show message of the day");
            configShowUserConnectedInDC = conf.Bind("Announcements", "ShowUserConnectedInDC", false, "Show in discord chat when users connecting");
            configShowUserConnectedInGameChat = conf.Bind("Announcements", "ShowUserConnectedInGameChat", false, "Show in game chat when users connecting");
            configShowUserDisConnectedInDC = conf.Bind("Announcements", "ShowUserDisConnectedInDC", false, "Show in discord chat when users disconnecting");
            configShowUserDisConnectedInGameChat = conf.Bind("Announcements", "ShowUserDisConnectedInGameChat", false, "Show in game chat when users disconnecting");
            
            configAnnounceTimer = conf.Bind("Announcements", "AnnounceTimer", 0f, "Time between messages in seconds");
            configAnnounceEnabled = conf.Bind("Announcements", "AnnounceEnabled", false, "Enable auto messages system");
            configAnnounceRandomOrder = conf.Bind("Announcements", "AnnounceRandomOrder", false, "Random order for messages");
            announce_entries = new List<string>();
            configAnnounceMessages = new List<ConfigEntry<string>>();
            for (int i = 0; i < 5; i++)
            { 
                ConfigEntry<string> AnnounceMessage = conf.Bind("Announcements", "AnnounceMessage" + (i+1), "", "Message for announce");
                configAnnounceMessages.Add(AnnounceMessage);
                if (AnnounceMessage.Value != "")
                {
                    announce_entries.Add(configAnnounceMessages[i].Value);
                    logger.LogWarning($"Registered message for announce /n{configAnnounceMessages[i].Value}");
                }
            }
            entries_count = announce_entries.Count;
            last_entry = 0;
            timer = configAnnounceTimer.Value;
            rnd = configAnnounceRandomOrder.Value;
            random = new Random();
            announcesEnabled = configAnnounceEnabled.Value;
            if (Announcements.instance != null)
            {
                Announcements.instance.Restart();
            }
        }
        public override bool Unload()
        {
            logger.LogWarning("Unpatching");
            harmony?.UnpatchSelf();
            Announcements._client.Dispose();
            return true;
        }
        /*
        [HarmonyPatch(typeof(ChatMessageSystem), "OnUpdate")]
        public static class ChatMessageSystemOnUpdatePatch {
            private static void Prefix(ChatMessageSystem __instance)
            {
                //var cms = __instance;
                try {
                    var entityManager = __instance.EntityManager;
                    var query = entityManager.CreateEntityQuery(Unity.EntitiesComponentType.ReadOnly<ChatMessageEvent>());
                    var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
                    foreach (var entity in entities) {
                        var msgEvent = entityManager.GetComponentData<ChatMessageEvent>(entity);
                        var fromCharacter = entityManager.GetComponentData<FromCharacter>(entity);
                        var user = entityManager.GetComponentData<User>(fromCharacter.User);
                        if (user.IsAdmin && msgEvent.MessageText.Equals(commandToReloadMessages))
                        {
                            ReloadConf();
                        }
                        else if (msgEvent.MessageType == ChatMessageType.Global)
                        {
                            Announcements.Post($"{user.CharacterName}: {msgEvent.MessageText}").GetAwaiter();
                        }
                    }
                }
                catch (Exception e) {
                    logger.LogError(e);
                }
            }
        }
        */
        [HarmonyPatch(typeof(ServerBootstrapSystem), "OnUserConnected")]
        public static class OnUserConnected_Patch
        {
            private static void Postfix(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
            {
                var entityManager = __instance.EntityManager;
                foreach (var sc in __instance._ApprovedUsersLookup)
                {
                    if (sc.UserEntity != null && sc.NetConnectionId != null && sc.NetConnectionId.Equals(netConnectionId))
                    {
                        var user = entityManager.GetComponentData<User>(sc.UserEntity);
                        var name = user.CharacterName;
                        if (name.IsEmpty) name = "New vampire";
                        if (user.IsAdmin) name = "[Admin] " + name;
                        if (configShowUserConnectedInDC.Value) Announcements.Post($"**{name} connected**").GetAwaiter();
                        if (configShowUserConnectedInGameChat.Value)
                            Announcements.PostInGame(entityManager, $"<b>{name} connected</b>").GetAwaiter();
                            //ServerChatUtils.SendSystemMessageToAllClients(entityManager, $"<b>{name} connected</b>");
                            if (configMOTDEnabled.Value) Announcements.PostInGameForUser(entityManager, user, configMOTD.Value).GetAwaiter();
                            //ServerChatUtils.SendSystemMessageToClient(entityManager, user, configMOTD.Value);
                    }
                    
                }
            }
        }
        [HarmonyPatch(typeof(ServerBootstrapSystem), "OnUserDisconnected")]
        public static class OnUserDisconnected_Patch
        {
            private static void Prefix(ServerBootstrapSystem __instance, NetConnectionId netConnectionId, ConnectionStatusChangeReason connectionStatusReason, string extraData)
            {
                if (connectionStatusReason != ConnectionStatusChangeReason.IncorrectPassword)
                {
                    var entityManager = __instance.EntityManager;
                    foreach (var sc in __instance._ApprovedUsersLookup)
                    {
                        if (sc.UserEntity != null && sc.NetConnectionId != null &&
                            sc.NetConnectionId.Equals(netConnectionId))
                        {
                            var user = entityManager.GetComponentData<User>(sc.UserEntity);
                            var name = user.CharacterName;
                            if (name.IsEmpty) name = "New vampire";
                            if (user.IsAdmin) name = "[Admin] " + name;
                            if (configShowUserDisConnectedInDC.Value) Announcements.Post($"**{name} disconnected**").GetAwaiter();
                            if (configShowUserDisConnectedInGameChat.Value)
                                Announcements.PostInGame(entityManager, $"<b>{name} disconnected</b>").GetAwaiter();
                            // ServerChatUtils.SendSystemMessageToAllClients(entityManager, $"<b>{name} disconnected</b>");
                        }
                    }
                }
            }
        }
        private static ServerNetworkLayer _serverNetworkLayer;
        [HarmonyPatch(typeof(ServerNetworkLayer), "Update")]
        public static class Update_Patch
        {
            private static void Postfix(ServerNetworkLayer __instance)
            {
                _serverNetworkLayer = __instance;
                harmony.Unpatch(typeof(ServerNetworkLayer).GetMethod("Update"),HarmonyPatchType.Postfix);
            }
        }
    }
}