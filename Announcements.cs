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
        public static ManualLogSource logger;
        public static Announcements instance;
        private float currenttime;
        private void Start()
        {
            logger = MainClass.logger;
            logger.LogWarning("Announcements start");
            instance = this;
            currenttime = 0;

            StartCoroutine( "CoroutineTest");

        }

        private void Update()
        {
            if (MainClass.announcesEnabled)
            {
                currenttime += Time.deltaTime;
                if ( currenttime >= MainClass.timer)
                {
                    logger.LogWarning($"posting announcement: {MainClass.announce_entries[MainClass.last_entry]}");
                    PostInGame(WorldUtility.FindWorld("Server").EntityManager, MainClass.announce_entries[MainClass.last_entry])
                        .GetAwaiter();
                    if (MainClass.rnd) MainClass.last_entry = MainClass.random.Next(0, MainClass.entries_count); else { 
                        MainClass.last_entry++; 
                        if (MainClass.last_entry == MainClass.entries_count) MainClass.last_entry = 0;
                    }

                    currenttime = 0;
                }            
            }
        }

        public void Restart()
        {
            currenttime = MainClass.timer;
            MainClass.logger.LogWarning("Announcements restart");
        }
        public void CoroutineTest()
        {
            MainClass.logger.LogWarning( "Start bot corutine" );
            if (MainClass.configEnableDiscordBot.Value)
            {
                RunBotAsync().GetAwaiter().GetResult();
            }
        }

        public static DiscordSocketClient _client;

        private async UniTask RunBotAsync()
        {
            DiscordSocketConfig config = new()
            {
                UseInteractionSnowflakeDate = false
            };
            _client = new DiscordSocketClient(config);
            string token = MainClass.configToken.Value;
            _client.Log += _client_Log;
            _client.MessageReceived += HandleCommandAsync;
            _client.Ready += Client_Ready;
            _client.SlashCommandExecuted += SlashCommandHandler;
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
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
                if (message.Author.IsBot || message.Channel.Id != MainClass.configChannelID.Value)
                    return Task.CompletedTask;
                logger.LogWarning($"message received from discord: {message.Author.Username}: {message.Content}");
                var entityManager = WorldUtility.FindWorld("Server").EntityManager;
                //ServerChatUtils.SendSystemMessageToAllClients(entityManager, $"[DC] {message.Author.Username}: {message.Content}");
                PostInGame(entityManager,
                    $"{MainClass.ingameMessagePrefix} {message.Author.Username}: {message.Content}").GetAwaiter();
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
                    if (sc.UserEntity != null && sc.NetConnectionId != null)
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
                if (MainClass.configEnableDiscordBot.Value) await (_client.GetChannel(MainClass.configChannelID.Value) as ITextChannel).SendMessageAsync(str);
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
                await Announcements._client.CreateGlobalApplicationCommandAsync(globalCommand.Build());
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