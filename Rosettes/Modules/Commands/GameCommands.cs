﻿using Discord;
using Discord.Interactions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rosettes.Core;

namespace Rosettes.Modules.Commands
{
	public class GameCommands : InteractionModuleBase<SocketInteractionContext>
	{
		[SlashCommand("csgo", "Status of Steam and CSGO matchmaking.")]
		public async Task CSGOStatus()
		{
			try
			{
				var data = await Global.HttpClient.GetStringAsync($"https://api.steampowered.com/ICSGOServers_730/GetGameServersStatus/v1/?key={Settings.SteamDevKey}");

				var DeserialziedObject = JsonConvert.DeserializeObject(data);
				if (DeserialziedObject == null)
				{
					await RespondAsync("Failed to retrieve status data.", ephemeral: true);
					return;
				}
				dynamic result = ((dynamic)DeserialziedObject).result;

				TimeSpan WaitTime = TimeSpan.FromSeconds(Convert.ToDouble(result.matchmaking.search_seconds_avg));

				EmbedBuilder steamStatus = await Global.MakeRosettesEmbed();

				steamStatus.Title = "Steam Status";

				steamStatus.AddField("Logon service", result.services.SessionsLogon, true);

				steamStatus.AddField("Steam Community", result.services.SteamCommunity, true);

				EmbedBuilder csgoStatus = await Global.MakeRosettesEmbed();

				csgoStatus.Title = "CS:GO Status";

				csgoStatus.AddField("Matchmaking", result.matchmaking.scheduler, true);
				csgoStatus.AddField("Online players", $"{result.matchmaking.online_players:N0}", true);
				csgoStatus.AddField("Online servers", $"{result.matchmaking.online_servers:N0}", true);
				csgoStatus.AddField("Searching game", $"{result.matchmaking.searching_players:N0}", true);
				csgoStatus.AddField("Average wait", $"{WaitTime.Minutes} minute{((WaitTime.Minutes != 1) ? 's' : null)}, {WaitTime.Seconds} seconds.\n", true);

				Embed[] embeds = { steamStatus.Build(), csgoStatus.Build() };

				await RespondAsync(embeds: embeds);
			}
			catch
			{
				await RespondAsync("Failed to fetch status data. This might mean steam is down.");
			}
		}

		[SlashCommand("ffxiv", "Status of FFXIV servers.")]
		public async Task XIVCheck(string checkServer = "NOTSPECIFIED")
		{
			string lobbyData;
			try
			{
				lobbyData = await Global.HttpClient.GetStringAsync("http://frontier.ffxiv.com/worldStatus/gate_status.json");
			}
			catch
			{
				await RespondAsync("Failed to retrieve datacenter list.", ephemeral: true);
				return;
			}
			var DeserializedLobbyObject = JsonConvert.DeserializeObject(lobbyData);
			if (DeserializedLobbyObject == null)
			{
				return;
			}

			dynamic lobby = DeserializedLobbyObject;

			string lobbyText = $"Lobby status    : {((lobby.status == 1) ? "Online" : "Offline")}";
			string serverText = "";
			if (checkServer == "NOTSPECIFIED")
			{
				serverText = $"For world status, please specify a datacenter name. (/ffxiv <name>)\n";
			}
			else
			{
				string datacenterData;
				string worldData;
				try
				{
					datacenterData = await Global.HttpClient.GetStringAsync($"https://xivapi.com/servers/dc?private_key={Settings.FFXIVApiKey}");
					worldData = await Global.HttpClient.GetStringAsync("http://frontier.ffxiv.com/worldStatus/current_status.json");
				}
				catch
				{
					await RespondAsync("Failed to retrieve datacenter data.", ephemeral: true);
					return;
				}

				var datacenterObj = JObject.Parse(datacenterData);
				var worldObj = JObject.Parse(worldData);

				if (datacenterObj == null || worldObj == null)
				{
					await RespondAsync("Failed to retrieve datacenter data.", ephemeral: true);
					return;
				}

				string searchTerm = checkServer.ToLower();
				List<string> serverNames = new();

				// This gets ugly really fast. Because of how frontier's xiv api formats the status response, we have to iterate all this crap
				// to figure out what servers we want to look at depending on the world given.
				// In other words, here we grab the datacenter object, with all it's servers are children token.
				foreach (var datacenter in datacenterObj.Cast<KeyValuePair<string, JToken>>().ToList())
				{
					if (datacenter.Key.ToLower() == searchTerm)
					{
						foreach(var server in datacenter.Value)
						{
							serverNames.Add(server.ToString());
						}
					}
				}
				if (!serverNames.Any())
				{
					serverText = "The specified datacenter was not found.\n";
				}
				else
				{
					// now that we have the name of the servers we care about, we go through the entire list taken off xiv's api
					// we compare text names and put them in when we have a hit.
					foreach (var world in worldObj.Cast<KeyValuePair<string, JToken>>().ToList())
					{
						if (serverNames.Contains(world.Key))
						{
							
							int spacing = 16 - world.Key.ToString().Length;
							string spacingText = "";
							for (int i = 0; i < spacing; i++)
							{
								spacingText += " ";
							}
							serverText += $"{world.Key}{spacingText}: { (((int)world.Value == 1) ? "Online" : "Offline")}\n";
						}
					}
				}
			}
			string text =
				$"```\n" +
				$"FFXIV Status:\n" +
				$"================\n" +
				$"{lobbyText}\n" +
				$"================\n" +
				$"{serverText}" +
				$"================\n" +
				$"```";
			await RespondAsync(text);
		}
	}
}