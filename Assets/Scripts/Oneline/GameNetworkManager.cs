using System;
using System.Collections.Generic;

using Mirror;

using UnityEngine;
using Unity.Services.Relay.Models;

namespace Utp
{
	public class GameNetworkManager : NetworkManager
	{
		private bool enemiesSpawned;

		// --- Lisää luokan alkuun kentät ---
		[Header("Co-op squad prefabs")]
		public GameObject unitHostPrefab;      // -> UnitSolo
		public GameObject unitClientPrefab;    // -> UnitSolo Player 2

		[Header("Enemy spawn (Co-op)")]
		public GameObject enemyPrefab;

		[Header("Spawn positions (world coords on your grid)")]
		public Vector3[] hostSpawnPositions = {
			new Vector3(0, 0, 0),
			new Vector3(2, 0, 0),
		};
		public Vector3[] clientSpawnPositions = {
			new Vector3(0, 0, 6),
			new Vector3(2, 0, 6),
		};
		public Vector3[] enemySpawnPositions = {
			new Vector3(4, 0, 8),
			new Vector3(6, 0, 8),
		};
		// --- --------------------------------

		private UtpTransport utpTransport;

		/// <summary>
		/// Server's join code if using Relay.
		/// </summary>
		public string relayJoinCode = "";

		public override void Awake()
		{
			base.Awake();

			utpTransport = GetComponent<UtpTransport>();

			string[] args = System.Environment.GetCommandLineArgs();
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
			enemiesSpawned = false;

			if (GameModeManager.SelectedMode == GameMode.CoOp)
				ServerSpawnEnemies();
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
			if (utpTransport == null)
			{
				utpTransport = GetComponent<UtpTransport>();
				if (utpTransport == null)
				{
					Debug.LogError("[NM] UtpTransport puuttuu samalta GameObjectilta!");
					return;
				}
			}

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
			if (utpTransport == null)
			{
				utpTransport = GetComponent<UtpTransport>();
				if (utpTransport == null)
				{
					Debug.LogError("[NM] UtpTransport puuttuu samalta GameObjectilta!");
					return;
				}
			}

			if (utpTransport == null)
			{
				utpTransport = GetComponent<UtpTransport>();
				if (utpTransport == null)
				{
					Debug.LogError("[NM] UtpTransport puuttuu samalta GameObjectilta!");
					return;
				}
			}

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
			if (utpTransport == null)
				utpTransport = GetComponent<UtpTransport>();
		}

		/// <summary>
		/// Tämä metodi spawnaa jokaiselle clientille oman Unitin ja tekee siitä heidän ohjattavan yksikkönsä.
		/// </summary>
		public override void OnServerAddPlayer(NetworkConnectionToClient conn)
		{
			/*
			Transform startPos = GetStartPosition(); // Spawn position (valinnainen)
			Vector3 spawnPosition = startPos != null ? startPos.position : Vector3.zero;

			GameObject player = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);

			NetworkServer.AddPlayerForConnection(conn, player); // antaa authorityn clientille
			*/

			// 1) Luo Mirrorin "player object" (EmptySquad) playerPrefab-slotista:
			// 1) luo player-object (EmptySquad)
			if (playerPrefab == null)
			{
				Debug.LogError("[NM] Player Prefab (EmptySquad) puuttuu!");
				return;
			}
			base.OnServerAddPlayer(conn);

			// 2) päätä host vs client
			bool isHost = conn.connectionId == 0;

			GameObject unitPrefab = isHost ? unitHostPrefab : unitClientPrefab;
			Vector3[] spawnPoints = isHost ? hostSpawnPositions : clientSpawnPositions;

			if (unitPrefab == null)
			{
				Debug.LogError($"[NM] {(isHost ? "unitHostPrefab" : "unitClientPrefab")} puuttuu!");
				return;
			}
			if (spawnPoints == null || spawnPoints.Length == 0)
			{
				Debug.LogError($"[NM] {(isHost ? "hostSpawnPositions" : "clientSpawnPositions")} ei ole asetettu!");
				return;
			}

			foreach (var pos in spawnPoints)
			{
				var go = Instantiate(unitPrefab, pos, Quaternion.identity);
				NetworkServer.Spawn(go, conn); // authority tälle pelaajalle
			}

			if (GameModeManager.SelectedMode == GameMode.CoOp && !enemiesSpawned)
			{ 
				ServerSpawnEnemies();
			}

		}

		[Server]
		void ServerSpawnEnemies()
		{
			if (!enemyPrefab || enemySpawnPositions == null || enemySpawnPositions.Length == 0)
			{
				Debug.LogWarning("[NM] EnemyPrefab/positions puuttuu");
				return;
			}

			foreach (var pos in enemySpawnPositions)
			{
				var e = Instantiate(enemyPrefab, pos, Quaternion.identity);
				NetworkServer.Spawn(e);
				Debug.Log("Enemy spawned at " + pos);
			}

			enemiesSpawned = true;
		}
	}
}
