﻿using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Rosettes.Modules.Commands;
using Rosettes.Modules.Engine;
using System.Reflection;

namespace Rosettes.Core
{
    public class InteractionManager
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _commands;
        private readonly IServiceProvider _services;

        public InteractionManager(DiscordSocketClient client, InteractionService commands, IServiceProvider services)
        {
            _client = client;
            _commands = commands;
            _services = services;
        }

        private async Task OnInteraction(SocketInteraction inter)
        {
            try
            {
                // get interaction context
                var context = new SocketInteractionContext(_client, inter);
                await _commands.ExecuteCommandAsync(context, _services);
            }
            catch (Exception ex)
            {
                Global.GenerateErrorMessage("InteractionManager", $"{ex}");

                // acknoweldge we crashed.
                await inter.RespondAsync("Sorry, there was an unknown error executing the command.", ephemeral: true);
            }
        }

        private async Task OnButtonClicked(SocketMessageComponent component)
        {
            string action = component.Data.CustomId;

            switch (action)
            {
                // rpg stuff
                case "fish":
                    await RpgEngine.CatchFishFunc(component, component.User);
                    break;
                case "inventory":
                    await RpgEngine.ShowInventoryFunc(component, component.User);
                    break;
                case "shop":
                    await RpgEngine.ShowShopFunc(component, component.User);
                    break;
                case "pets":
                    await RpgEngine.ShowPets(component, component.User);
                    break;


                // music stuff
                case "music_toggle": case "music_skip": case "music_stop":
                    if (component.GuildId is ulong guild_id)
                    {
                        var dbGuild = GuildEngine.GetDBGuildById(guild_id);
                        if (dbGuild != null)
                        {
                            var guild = dbGuild.GetDiscordSocketReference();
                            if (guild != null)
                            {
                                var dbUser = await UserEngine.GetDBUser(component.User);
                                EmbedBuilder embed = await Global.MakeRosettesEmbed(dbUser);
                                embed.Title = "Music Player";
                                if (action == "music_toggle")
                                {
                                    embed.Description = await MusicEngine.ToggleAsync(guild);
                                    await component.RespondAsync(embed: embed.Build(), components: MusicCommands.GetMusicButtons());
                                }
                                if (action == "music_skip")
                                {
                                    embed.Description = await MusicEngine.SkipTrackAsync(guild);
                                    await component.RespondAsync(embed: embed.Build(), components: MusicCommands.GetMusicButtons());
                                }
                                if (action == "music_stop")
                                {
                                    embed.Description = await MusicEngine.StopAsync(guild);
                                    await component.RespondAsync(embed: embed.Build());
                                }
                            }
                        }
                    }
                    break;



                // if nothing else, it's poll stuff
                default:
                    await component.RespondAsync(await PollEngine.VoteInPoll(component.User.Id, component.Message, component.Data.CustomId), ephemeral: true);
                    break;
            }
        }

        private async Task OnMenuSelectionMade(SocketMessageComponent component)
        {
            if (component.Data.CustomId is "buy" or "sell") {
                await RpgEngine.ShopAction(component);
            }
            if (component.Data.CustomId is "make")
            {
                await RpgEngine.CraftAction(component);
            }
            if (component.Data.CustomId is "defaultPet")
            {
                await RpgEngine.SetDefaultPet(component);
            }
        }

        private Task OnCommandExecuted(SlashCommandInfo arg1, Discord.IInteractionContext arg2, IResult arg3)
        {
            return Task.CompletedTask;
        }

        public async Task SetupAsync()
        {
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            await _commands.RegisterCommandsGloballyAsync();

            _client.InteractionCreated += OnInteraction;

            _client.ButtonExecuted += OnButtonClicked;

            _client.SelectMenuExecuted += OnMenuSelectionMade;

            _commands.SlashCommandExecuted += OnCommandExecuted;
        }
    }
}
