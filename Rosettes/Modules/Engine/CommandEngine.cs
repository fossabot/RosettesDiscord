﻿using Dapper;
using Discord;
using Discord.Commands;
using MySqlConnector;
using Rosettes.Core;
using Rosettes.Modules.Commands;

namespace Rosettes.Modules.Engine
{
    public static class CommandEngine
    {
        private static readonly CommandService _commands = ServiceManager.GetService<CommandService>();
        public static readonly Dictionary<string, int> CommandUsage = new();

        public static async Task LoadCommands()
        {
            // Load all the commands from their modules
            // The order in which commands are listed is the ordewr
            // in which they are listed.
            await _commands.AddModuleAsync<UtilityCommands>(null);
            await _commands.AddModuleAsync<RandomCommands>(null);
            await _commands.AddModuleAsync<MusicCommands>(null);
            await _commands.AddModuleAsync<GameCommands>(null);
            await _commands.AddModuleAsync<DumbCommands>(null);
            await _commands.AddModuleAsync<AdminCommands>(null);

            // always load ElevatedCommands last.
            await _commands.AddModuleAsync<ElevatedCommands>(null);
        }

        public static async Task HandleCommand(SocketCommandContext context, int argPos)
        {
            var user = await UserEngine.GetDBUser(context.User);
            if (user.CanUseCommand(context.Guild))
            {
                string usedCommand;
                // get the name of the used command and count it for usage analytics
                // if it contains a space, that means the command has arguments. delete the arguments
                if (context.Message.Content.Contains(' '))
                {
                    usedCommand = context.Message.Content[0..context.Message.Content.IndexOf(" ")];
                } else
                {
                    usedCommand = context.Message.Content;
                }
                var result = await _commands.ExecuteAsync(context: context, argPos: argPos, services: ServiceManager.Provider);
                if (result.IsSuccess)
                {
                    ReportUse(usedCommand);
                }
            }
            else
            {
                await context.Message.AddReactionAsync(new Emoji("⌚"));
            }
        }

        public static void CreateCommandPage()
        {
            if (!Directory.Exists("/var/www/html/rosettes/"))
            {
                Directory.CreateDirectory("/var/www/html/rosettes/");
            }

            string webContents =
                @"<style>
                    body {
                        font-family: -apple-system, BlinkMacSystemFont, Segoe UI, Roboto, Oxygen, Ubuntu,
		                Cantarell, Fira Sans, Droid Sans, Helvetica Neue, sans-serif;
	                    line-height: 1.6;
	                    font-size: 18px;
	                    color: #DDDDDD;
	                    background-color: darkslategray;
                    }
                    .container {
                        max-width: 48rem;
	                    padding: 1rem;
	                    margin: 0rem auto;
                    }
                  </style>
                  <div class='container'>
            ";

            webContents += $"<p><small>This page is autogenerated by Rosettes every time it restarts. Last update: {DateTime.UtcNow:ddd, dd MMM yyy; HH:mm:ss} GMT</small></p>\n";

            ModuleInfo? currModule = null;
            var comms = ServiceManager.GetService<CommandService>();
            foreach (CommandInfo singleCommand in comms.Commands)
            {
                if (singleCommand.Module.Name == "ElevatedCommands") break;
                if (currModule == null || currModule.Name != singleCommand.Module.Name)
                {
                    currModule = singleCommand.Module;
                    webContents += $"<hr>\n<p><b>{currModule.Summary}</b></p>\n";
                }
                webContents += $"<p><b>{Settings.Prefix}{singleCommand.Name}</b><br>\n";
                if (singleCommand.Summary != null)
                {
                    webContents += $"{singleCommand.Summary}</p>\n";
                }
                else
                {
                    webContents += $"&nbsp;</p>\n";
                }
            }
            webContents += "</div>";
            using var writer = File.CreateText("/var/www/html/rosettes/commands.html");

            writer.Write(webContents);

            writer.Close();
        }

        public static void ReportUse(string Command)
        {
            if (!CommandEngine.CommandUsage.ContainsKey(Command))
            {
                CommandEngine.CommandUsage.Add(Command, 1);
            }
            else
            {
                CommandEngine.CommandUsage[Command]++;
            }
        }

        public static async void SyncWithDatabase()
        {
            foreach (var cmd in CommandEngine.CommandUsage)
            {
                var db = new MySqlConnection(Settings.Database.ConnectionString);

                var sql = @"SELECT count(1) FROM command_analytics WHERE command=@Command";

                bool result = await db.ExecuteScalarAsync<bool>(sql, new { Command = cmd.Key });

                if (result)
                {
                    sql = @"UPDATE command_analytics SET uses=uses + @LoggedUses WHERE command=@Command";
                }
                else
                {
                    sql = @"INSERT INTO command_analytics (command, uses)
                        VALUES(@Command, @LoggedUses)";
                }
                try
                {
                    await db.ExecuteAsync(sql, new { Command = cmd.Key, LoggedUses = cmd.Value });
                }
                catch (Exception ex)
                {
                    Global.GenerateErrorMessage("commandEngine-usage", $"sqlException code {ex.Message}");
                    return;
                }
                
            }
            CommandEngine.CommandUsage.Clear();
        }
    }
}
