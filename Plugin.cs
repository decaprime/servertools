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
        public static ManualLogSource Logger;
        private static Harmony _harmony;

        public static ConfigFile Conf;

        public static ConfigEntry<bool> ConfigEnableDiscordBot;
        //private static ConfigEntry<ulong> configGuildID;
        public static ConfigEntry<ulong> ConfigChannelID;
        public static ConfigEntry<string> ConfigToken;
        private static ConfigEntry<string> _configMotd;
        private static ConfigEntry<bool> _configMotdEnabled;
        private static ConfigEntry<bool> _configShowUserConnectedInDc;
        private static ConfigEntry<bool> _configShowUserConnectedInGameChat;
        private static ConfigEntry<bool> _configShowUserDisConnectedInDc;
        private static ConfigEntry<bool> _configShowUserDisConnectedInGameChat;
        public static ConfigEntry<float> ConfigAnnounceTimer;
        private static ConfigEntry<bool> _configAnnounceEnabled;
        public static ConfigEntry<bool> ConfigAnnounceRandomOrder;
        public static ConfigEntry<string> ConfigIngameMessagePrefix;
        public static ConfigEntry<string> ConfigCommandToReloadMessages;
        
        
        public static List<string> AnnounceEntries;
        private static List<ConfigEntry<string>> _configAnnounceMessages;
        
        
        public static int EntriesCount;
        public static int LastEntry;
        public static float Timer;
        public static bool Rnd;
        public static Random Random;
        public static bool AnnouncesEnabled;
        public static string IngameMessagePrefix;
        public static string CommandToReloadMessages;
        
        
        public override void Load()
        {
            Logger = Log;
            Conf = Config;
            Logger.LogWarning("Hello, world!!!");
            ConfigEnableDiscordBot = Conf.Bind("General", "EnableDiscordBot", false, "Enable discord features");
            ConfigChannelID = Conf.Bind("General", "ChannelID", (ulong)0, "Channel ID for broadcast");
            ConfigToken = Conf.Bind("General", "Token", "YOUR_TOKEN", "Bot Token from https://discord.com/developers/applications");
            ConfigIngameMessagePrefix = Conf.Bind("General", "IngameMessagePrefix", "[DC]", "Prefix for ingame messages from discord");
            ConfigCommandToReloadMessages = Conf.Bind("General", "CommandToReloadMessages", "!rm", "Chat command which allow reload auto messages from config");
            IngameMessagePrefix = ConfigIngameMessagePrefix.Value;
            CommandToReloadMessages = ConfigCommandToReloadMessages.Value;
            ReloadConf();
            Chat.OnChatMessage += HandleChatMessage;
            ClassInjector.RegisterTypeInIl2Cpp<Announcements>();
            AddComponent<Announcements>();
            Logger.LogWarning("Patching");
            _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
            Logger.LogWarning("Patching done");
        }
        
        public void HandleChatMessage(VChatEvent ev)
        {
            if (!ev.Cancelled)
            {
                if (ev.User.IsAdmin && ev.Message.Equals(CommandToReloadMessages))
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
            Logger.LogWarning("conf.Reload();");
            Conf.Reload();
            _configMotd = Conf.Bind("Announcements", "MOTD", "Hello here is MOTD", "Set message of the day");
            _configMotdEnabled = Conf.Bind("Announcements", "MOTDEnabled", false, "Show message of the day");
            _configShowUserConnectedInDc = Conf.Bind("Announcements", "ShowUserConnectedInDC", false, "Show in discord chat when users connecting");
            _configShowUserConnectedInGameChat = Conf.Bind("Announcements", "ShowUserConnectedInGameChat", false, "Show in game chat when users connecting");
            _configShowUserDisConnectedInDc = Conf.Bind("Announcements", "ShowUserDisConnectedInDC", false, "Show in discord chat when users disconnecting");
            _configShowUserDisConnectedInGameChat = Conf.Bind("Announcements", "ShowUserDisConnectedInGameChat", false, "Show in game chat when users disconnecting");
            
            ConfigAnnounceTimer = Conf.Bind("Announcements", "AnnounceTimer", 0f, "Time between messages in seconds");
            _configAnnounceEnabled = Conf.Bind("Announcements", "AnnounceEnabled", false, "Enable auto messages system");
            ConfigAnnounceRandomOrder = Conf.Bind("Announcements", "AnnounceRandomOrder", false, "Random order for messages");
            AnnounceEntries = new List<string>();
            _configAnnounceMessages = new List<ConfigEntry<string>>();
            for (int i = 0; i < 5; i++)
            { 
                ConfigEntry<string> announceMessage = Conf.Bind("Announcements", "AnnounceMessage" + (i+1), "", "Message for announce");
                _configAnnounceMessages.Add(announceMessage);
                if (announceMessage.Value != "")
                {
                    AnnounceEntries.Add(_configAnnounceMessages[i].Value);
                    Logger.LogWarning($"Registered message for announce /n{_configAnnounceMessages[i].Value}");
                }
            }
            EntriesCount = AnnounceEntries.Count;
            LastEntry = 0;
            Timer = ConfigAnnounceTimer.Value;
            Rnd = ConfigAnnounceRandomOrder.Value;
            Random = new Random();
            AnnouncesEnabled = _configAnnounceEnabled.Value;
            if (Announcements.Instance != null)
            {
                Announcements.Instance.Restart();
            }
        }
        public override bool Unload()
        {
            Logger.LogWarning("Unpatching");
            _harmony?.UnpatchSelf();
            Announcements.Client.Dispose();
            return true;
        }
        
        [HarmonyPatch(typeof(ServerBootstrapSystem), "OnUserConnected")]
        public static class OnUserConnectedPatch
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
                        if (_configShowUserConnectedInDc.Value) Announcements.Post($"**{name} connected**").GetAwaiter();
                        if (_configShowUserConnectedInGameChat.Value)
                            Announcements.PostInGame(entityManager, $"<b>{name} connected</b>").GetAwaiter();
                            //ServerChatUtils.SendSystemMessageToAllClients(entityManager, $"<b>{name} connected</b>");
                            if (_configMotdEnabled.Value) Announcements.PostInGameForUser(entityManager, user, _configMotd.Value).GetAwaiter();
                            //ServerChatUtils.SendSystemMessageToClient(entityManager, user, configMOTD.Value);
                    }
                    
                }
            }
        }
        [HarmonyPatch(typeof(ServerBootstrapSystem), "OnUserDisconnected")]
        public static class OnUserDisconnectedPatch
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
                            if (_configShowUserDisConnectedInDc.Value) Announcements.Post($"**{name} disconnected**").GetAwaiter();
                            if (_configShowUserDisConnectedInGameChat.Value)
                                Announcements.PostInGame(entityManager, $"<b>{name} disconnected</b>").GetAwaiter();
                            // ServerChatUtils.SendSystemMessageToAllClients(entityManager, $"<b>{name} disconnected</b>");
                        }
                    }
                }
            }
        }
        private static ServerNetworkLayer _serverNetworkLayer;
        [HarmonyPatch(typeof(ServerNetworkLayer), "Update")]
        public static class UpdatePatch
        {
            private static void Postfix(ServerNetworkLayer __instance)
            {
                _serverNetworkLayer = __instance;
                _harmony.Unpatch(typeof(ServerNetworkLayer).GetMethod("Update"),HarmonyPatchType.Postfix);
            }
        }
    }
}