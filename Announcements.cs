using System;
using System.Collections;
using System.Threading.Tasks;
using BepInEx.Logging;
using Cysharp.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using ProjectM;
using ProjectM.Network;
using Unity.Entities;
using UnityEngine;

namespace servertools
{

    public class Announcements : MonoBehaviour
    {
        private static ManualLogSource logger;
        public static Announcements Instance;
        private float _currenttime;
        private void Start()
        {
            logger = MainClass.logger;
            logger.LogWarning("Announcements start");
            Instance = this;
            _currenttime = 0;

            StartCoroutine( "CoroutineTest");

        }

        private void Update()
        {
            if (MainClass.AnnouncesEnabled)
            {
                _currenttime += Time.deltaTime;
                if ( _currenttime >= MainClass.Timer)
                {
                    logger.LogWarning($"posting announcement: {MainClass.AnnounceEntries[MainClass.LastEntry]}");
                    PostInGame(WorldUtility.FindWorld("Server").EntityManager, MainClass.AnnounceEntries[MainClass.LastEntry])
                        .GetAwaiter();
                    if (MainClass.Rnd) MainClass.LastEntry = MainClass.Random.Next(0, MainClass.EntriesCount); else { 
                        MainClass.LastEntry++; 
                        if (MainClass.LastEntry == MainClass.EntriesCount) MainClass.LastEntry = 0;
                    }

                    _currenttime = 0;
                }            
            }
        }

        public void Restart()
        {
            _currenttime = MainClass.Timer;
            MainClass.logger.LogWarning("Announcements restart");
        }
        public void CoroutineTest()
        {
            MainClass.logger.LogWarning( "Start bot corutine" );
            if (MainClass.ConfigEnableDiscordBot.Value)
            {
                RunBotAsync().GetAwaiter().GetResult();
            }
        }

        public static DiscordSocketClient Client;

        private async UniTask RunBotAsync()
        {
            DiscordSocketConfig config = new()
            {
                UseInteractionSnowflakeDate = false
            };
            Client = new DiscordSocketClient(config);
            string token = MainClass.ConfigToken.Value;
            Client.Log += _client_Log;
            Client.MessageReceived += HandleCommandAsync;
            Client.Ready += Client_Ready;
            Client.SlashCommandExecuted += SlashCommandHandler;
            await Client.LoginAsync(TokenType.Bot, token);
            await Client.StartAsync();
        }
        private Task _client_Log(LogMessage arg)
        {
            logger.LogWarning(arg.Message);
            return Task.CompletedTask;
        }
        private Task HandleCommandAsync(SocketMessage arg)
        {
            try
            {
                var message = arg as SocketUserMessage;
                if (message.Author.IsBot || message.Channel.Id != MainClass.ConfigChannelID.Value)
                    return Task.CompletedTask;
                logger.LogWarning($"message received from discord: {message.Author.Username}: {message.Content}");
                var entityManager = WorldUtility.FindWorld("Server").EntityManager;
                //ServerChatUtils.SendSystemMessageToAllClients(entityManager, $"[DC] {message.Author.Username}: {message.Content}");
                PostInGame(entityManager,
                    $"{MainClass.IngameMessagePrefix} {message.Author.Username}: {message.Content}").GetAwaiter();
            }
            catch (Exception e)
            {
                logger.LogError(e);
            }
            return Task.CompletedTask;
        }
        public static async UniTask PostInGame(EntityManager entityManager, string str)
        {
            ServerChatUtils.SendSystemMessageToAllClients(entityManager, str);
            await UniTask.Yield();
            /*try
            {
                var bs = WorldUtility.FindWorld("Server").GetExistingSystem<ServerBootstrapSystem>();
                foreach (var sc in bs._ApprovedUsersLookup)
                {
                    if (sc.UserEntity != Entity.Null && sc.NetConnectionId != null)
                    {
                        var user = entityManager.GetComponentData<User>(sc.UserEntity);
                        if (_serverNetworkLayer._ConnectionIdToUserState[sc.NetConnectionId].ConnectState == ServerNetworkLayer.UserConnectState.Connected)
                        {
                            PostInGameForUser(entityManager, user, str).GetAwaiter();
                        }
                        else
                        {
                            logger.LogWarning($"the message cannot be received by the player {user.CharacterName} because he has not connected yet ");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e);
            }
            await UniTask.Yield();
            */
        }
        public static async UniTask Post(string str)
        {
            try
            {
                if (MainClass.ConfigEnableDiscordBot.Value) await (Client.GetChannel(MainClass.ConfigChannelID.Value) as ITextChannel).SendMessageAsync(str);
            }
            catch (Exception e)
            {
                logger.LogError(e);
            }
        }

        public static async UniTask PostInGameForUser(EntityManager entityManager, User user, string str)
        {
            try
            {
                ServerChatUtils.SendSystemMessageToClient(entityManager, user, str);
            }
            catch (Exception e)
            {
                logger.LogError(e);
            }
            await UniTask.Yield();
        }

        private async Task Client_Ready()
        {
            var globalCommand = new SlashCommandBuilder();
            globalCommand.WithName("status");
            globalCommand.WithDescription("Show server status");
            try
            {
                await Announcements.Client.CreateGlobalApplicationCommandAsync(globalCommand.Build());
            }
            catch(HttpException exception)
            {
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
                logger.LogError(json);
            }
        }
        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            try
            {            
                if (command.CommandName.Equals("status"))
                {
                    EmbedBuilder embedBuiler = new EmbedBuilder();
                    embedBuiler.WithDescription(" ");
                    embedBuiler.WithTitle("**Server status**");
                    embedBuiler.WithColor(0x18, 0xf7, 0xf7);

                    string str="";
                    int count=0;
                    var bs = WorldUtility.FindWorld("Server").GetExistingSystem<ServerBootstrapSystem>();
                    var entityManager = WorldUtility.FindWorld("Server").EntityManager;
                    foreach (var sc in bs._ApprovedUsersLookup)
                    {
                        if (sc.UserEntity != null && sc.NetConnectionId != null)
                        {
                            count++;
                            str += $"{entityManager.GetComponentData<User>(sc.UserEntity).CharacterName}, ";
                        }
                    }

                    if (count == 0) str += "------------";
                    embedBuiler.AddField($"{count} players online", str.Substring(0, str.Length-2));
                    await command.RespondAsync(embed: embedBuiler.Build());
                }

            }
            catch (Exception e)
            {
                logger.LogError(e);
                throw;
            }
        }
    }
}