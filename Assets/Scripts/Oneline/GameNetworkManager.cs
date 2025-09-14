using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Unity.Services.Relay.Models;

namespace Utp
{
	[RequireComponent(typeof(UtpTransport))]
	public class GameNetworkManager : NetworkManager
	{
		public static GameNetworkManager Instance { get; private set; }
		private UtpTransport utpTransport;

		/// <summary>
		/// Server's join code if using Relay.
		/// </summary>
		public string relayJoinCode = "";


		public override void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;

			base.Awake();

			utpTransport = GetComponent<UtpTransport>();

			string[] args = Environment.GetCommandLineArgs();
			for (int key = 0; key < args.Length; key++)
			{
				if (args[key] == "-port")
				{
					if (key + 1 < args.Length)
					{
						string value = args[key + 1];

						try
						{
							utpTransport.Port = ushort.Parse(value);
						}
						catch
						{
							UtpLog.Warning($"Unable to parse {value} into transport Port");
						}
					}
				}
			}
		}

		public override void OnStartServer()
		{
			base.OnStartServer();
			SpawnUnitsCoordinator.Instance.SetEnemiesSpawned(false);

			if (GameModeManager.SelectedMode == GameMode.CoOp)
			{
				ServerSpawnEnemies();
			}

			// DODO PvP pelin käynnistys
			else if (GameModeManager.SelectedMode == GameMode.Versus)
			{
				
			}


		}

		/// <summary>
		/// Get the port the server is listening on.
		/// </summary>
		/// <returns>The port.</returns>
		public ushort GetPort()
		{
			return utpTransport.Port;
		}

		/// <summary>
		/// Get whether Relay is enabled or not.
		/// </summary>
		/// <returns>True if enabled, false otherwise.</returns>
		public bool IsRelayEnabled()
		{
			return utpTransport.useRelay;
		}

		/// <summary>
		/// Ensures Relay is disabled. Starts the server, listening for incoming connections.
		/// </summary>
		public void StartStandardServer()
		{
			utpTransport.useRelay = false;
			StartServer();
		}

		/// <summary>
		/// Ensures Relay is disabled. Starts a network "host" - a server and client in the same application
		/// </summary>
		public void StartStandardHost()
		{
			utpTransport.useRelay = false;
			StartHost();
		}

		/// <summary>
		/// Gets available Relay regions.
		/// </summary>
		/// 
		public void GetRelayRegions(Action<List<Region>> onSuccess, Action onFailure)
		{
			utpTransport.GetRelayRegions(onSuccess, onFailure);
		}

		/// <summary>
		/// Ensures Relay is enabled. Starts a network "host" - a server and client in the same application
		/// </summary>
		public void StartRelayHost(int maxPlayers, string regionId = null)
		{
			utpTransport.useRelay = true;
			utpTransport.AllocateRelayServer(maxPlayers, regionId,
			(string joinCode) =>
			{
				relayJoinCode = joinCode;
				Debug.LogError($"Relay join code: {joinCode}");
				StartHost();
			},
			() =>
			{
				UtpLog.Error($"Failed to start a Relay host.");
			});
		}

		/// <summary>
		/// Ensures Relay is disabled. Starts the client, connects it to the server with networkAddress.
		/// </summary>
		public void JoinStandardServer()
		{
			utpTransport.useRelay = false;
			StartClient();
		}

		/// <summary>
		/// Ensures Relay is enabled. Starts the client, connects to the server with the relayJoinCode.
		/// </summary>
		public void JoinRelayServer()
		{
			utpTransport.useRelay = true;
			utpTransport.ConfigureClientWithJoinCode(relayJoinCode,
			() =>
			{
				StartClient();
			},
			() =>
			{
				UtpLog.Error($"Failed to join Relay server.");
			});
		}

		public override void OnValidate()
		{
			base.OnValidate();
		}

		/// <summary>
		/// Tämä metodi spawnaa jokaiselle clientille oman Unitin ja tekee siitä heidän ohjattavan yksikkönsä.
		/// </summary>
		public override void OnServerAddPlayer(NetworkConnectionToClient conn)
		{

			if (playerPrefab == null)
			{
				Debug.LogError("[NM] Player Prefab (EmptySquad) puuttuu!");
				return;
			}
			base.OnServerAddPlayer(conn);

			// 2) päätä host vs client
			bool isHost = conn.connectionId == 0;

			// 3) spawnaa pelaajan yksiköt ja anna authority niihin
			var units = SpawnUnitsCoordinator.Instance.SpawnPlayersForNetwork(isHost);
			foreach (var unit in units)
			{
				NetworkServer.Spawn(unit, conn); // authority tälle pelaajalle
			}

			// päivitä pelaajamäärä koordinaattorille
			var coord = NetTurnManager.Instance;
			//var coord = CoopTurnCoordinator.Instance;
			if (coord != null)
				coord.ServerUpdateRequiredCount(NetworkServer.connections.Count);

			// --- VERSUS (PvP) — host aloittaa ---
			if (GameModeManager.SelectedMode == GameMode.Versus)
			{
				var pc = conn.identity != null ? conn.identity.GetComponent<PlayerController>() : null;
				if (pc != null && PvPTurnCoordinator.Instance != null)
				{
					// Rekisteröi pelaaja PvP-vuoroon (host saa aloitusvuoron PvPTurnCoordinatorissa)
					PvPTurnCoordinator.Instance.ServerRegisterPlayer(pc);
				}
				else
				{
					Debug.LogWarning("[NM] PvP rekisteröinti epäonnistui: PlayerController tai PvPTurnCoordinator puuttuu.");
				}
			}

		}

		[Server]
		void ServerSpawnEnemies()
		{

			// Pyydä SpawnUnitsCoordinatoria luomaan viholliset
			var enemies = SpawnUnitsCoordinator.Instance.SpawnEnemies();

			// Synkronoi viholliset verkkoon Mirrorin avulla
			foreach (var enemy in enemies)
			{
				if (enemy != null)
				{
					NetworkServer.Spawn(enemy);
					Debug.Log($"[NM] Enemy spawned on network: {enemy.transform.position}");
				}
			}
		}


		public override void OnServerDisconnect(NetworkConnectionToClient conn)
		{
			base.OnServerDisconnect(conn);
			// päivitä pelaajamäärä koordinaattorille
			var coord = NetTurnManager.Instance;
			//var coord = CoopTurnCoordinator.Instance;
			if (coord != null)
				coord.ServerUpdateRequiredCount(NetworkServer.connections.Count);
		}

		public bool IsNetworkActive()
		{
			return GetNetWorkServerActive() || GetNetWorkClientConnected();
		}

		public bool GetNetWorkServerActive()
		{
			return NetworkServer.active;
		}

		public bool GetNetWorkClientConnected()
		{
			return NetworkClient.isConnected;
		}

		public NetworkConnection NetWorkClientConnection()
		{
			return NetworkClient.connection;
		}
		
		public void NetworkDestroy(GameObject go)
		{
			NetworkServer.Destroy(go);
		}
	}
}
