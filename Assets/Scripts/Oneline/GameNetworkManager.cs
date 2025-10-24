using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using Unity.Services.Relay.Models;
using UnityEngine.SceneManagement;

namespace Utp
{
	[RequireComponent(typeof(UtpTransport))]
	public class GameNetworkManager : NetworkManager
	{
		public static GameNetworkManager Instance { get; private set; }

		private readonly List<NetworkConnectionToClient> _pendingConns = new();

		[SerializeField] private int hideJoinCodeAfterConnections = 2; // Host + 1 client
		
		public int HideJoinCodeAfterConnections => Mathf.Max(1, hideJoinCodeAfterConnections);

		private UtpTransport utpTransport;

		/// <summary>
		/// Server's join code if using Relay.
		/// </summary>
		public string relayJoinCode = "";


		public override void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Debug.LogError("There's more than one GameNetworkManager! " + transform + " - " + Instance);
				Destroy(gameObject);
				return;
			}
			Instance = this;

			base.Awake();
			autoCreatePlayer = false;

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
			LevelLoader.LevelReady += OnLevelReady_Server;

			SpawnUnitsCoordinator.Instance.SetEnemiesSpawned(false);

			if (GameModeManager.SelectedMode == GameMode.CoOp)
			{
				ServerSpawnEnemies();
			}
		}

		public override void OnStopServer()
		{
			LevelLoader.LevelReady -= OnLevelReady_Server;
			base.OnStopServer();
		}
		/*
		void OnEnable()
		{
			LevelLoader.LevelReady += OnLevelReady_Server;
		}
		*/
		
		void OnDisable() { LevelLoader.LevelReady -= OnLevelReady_Server; }

		[ServerCallback]
		public override void OnDestroy()
		{
			LevelLoader.LevelReady -= OnLevelReady_Server;
		}


		[Server]
		private void OnLevelReady_Server(Scene mapScene)
		{
			if (!NetworkServer.active) return;

			Debug.Log("[GameNetworkManager] OnLevelReady_Server - Processing level ready");

			// 1) Ensilataus: pending-jonon finalisointi
			foreach (var c in _pendingConns)
				if (c != null) ServerFinalizeAddPlayer(c);
			_pendingConns.Clear();

			// 2) RELOAD: spawn kaikille aktiivisille conneille
			Debug.Log($"[GameNetworkManager] Active connections: {NetworkServer.connections.Count}");
			
			foreach (var kv in NetworkServer.connections)
			{
				var conn = kv.Value;
				if (conn == null) continue;

				Debug.Log($"[GameNetworkManager] Processing conn {conn.connectionId}, identity: {(conn.identity != null ? conn.identity.name : "NULL")}");

				// Jos identity on null, luo se uudelleen
				if (conn.identity == null)
				{
					Debug.Log($"[GameNetworkManager] Creating new PlayerController for conn {conn.connectionId}");
					if (playerPrefab != null)
					{
						base.OnServerAddPlayer(conn);
					}
					else
					{
						Debug.LogError("[GameNetworkManager] PlayerPrefab is null!");
						continue;
					}
				}

				// Tarkista onko uniteja
				uint ownerId = conn.identity != null ? conn.identity.netId : 0u;
				bool hasUnits = ownerId != 0 && HasOwnedUnit(ownerId);
				
				Debug.Log($"[GameNetworkManager] Conn {conn.connectionId} - ownerId: {ownerId}, hasUnits: {hasUnits}");

				if (!hasUnits)
				{
					bool isHost = conn == NetworkServer.localConnection;
					Debug.Log($"[GameNetworkManager] Spawning units for {(isHost ? "HOST" : "CLIENT")} (conn {conn.connectionId})");
					
					var units = SpawnUnitsCoordinator.Instance?.SpawnPlayersForNetwork(conn, isHost);
					if (units != null)
					{
						Debug.Log($"[GameNetworkManager] Successfully spawned {units.Length} units for conn {conn.connectionId}");
					}
					else
					{
						Debug.LogWarning($"[GameNetworkManager] Failed to spawn units for conn {conn.connectionId}");
					}
				}
			}

			// 3) Viholliset jos tarvitaan
			if (GameModeManager.SelectedMode == GameMode.CoOp)
			{
				if (!SpawnUnitsCoordinator.Instance.AreEnemiesSpawned())
				{
					Debug.Log("[GameNetworkManager] Spawning enemies for Co-op mode");
					ServerSpawnEnemies();
				}
			}

			// 4) Käynnistä uusi matsi
			LevelGrid.Instance?.RebuildOccupancyFromScene();
			EdgeBaker.Instance?.BakeAllEdges();
			NetTurnManager.Instance?.ServerResetAndBegin();
			
			Debug.Log("[GameNetworkManager] OnLevelReady_Server - Complete");
		}


		[Server]
		private bool HasOwnedUnit(uint ownerNetId)
		{
			var units = FindObjectsByType<Unit>(FindObjectsSortMode.None);
			for (int i = 0; i < units.Length; i++)
				if (units[i] && units[i].OwnerId == ownerNetId)
					return true;
			return false;
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
			//	Debug.LogError($"Relay join code: {joinCode}");
				Debug.Log($"Relay join code: {joinCode}");
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
		/// Make sure that the clien sends a AddPlayer request once the scene is loaded.
		/// </summary>
		public override void OnClientSceneChanged()
		{
			base.OnClientSceneChanged();

			Debug.Log($"[NM] OnClientSceneChanged - ready: {NetworkClient.ready}");
			
			if (!NetworkClient.ready) 
				NetworkClient.Ready();

			// Pyydä pelaaja jos ei vielä ole
			if (NetworkClient.connection != null && NetworkClient.connection.identity == null)
			{
				Debug.Log("[NM] OnClientSceneChanged requesting player via AddPlayer()");
				NetworkClient.AddPlayer();
			}
		}
		
		public override void OnClientConnect()
		{
			base.OnClientConnect();
			
			Debug.Log($"[NM] OnClientConnect - NetworkClient.ready: {NetworkClient.ready}");
			
			// Varmista että client on ready
			if (!NetworkClient.ready)
			{
				NetworkClient.Ready();
			}
			
			// Pyydä pelaaja heti
			if (NetworkClient.connection != null && NetworkClient.connection.identity == null)
			{
				Debug.Log("[NM] Client requesting player via AddPlayer()");
				NetworkClient.AddPlayer();
			}
		}

		public override void OnStopClient()
		{
			base.OnStopClient();
		}

		public override void OnClientDisconnect()
		{
			base.OnClientDisconnect();
		}

		/// <summary>
		/// Tämä metodi spawnaa jokaiselle clientille oman Unitin ja tekee siitä heidän ohjattavan yksikkönsä.
		/// </summary>
		public override void OnServerAddPlayer(NetworkConnectionToClient conn)
		{
			Debug.Log($"[NM] ===== OnServerAddPlayer called for conn {conn.connectionId} =====");
			Debug.Log($"[NM] LevelLoader.IsServerLevelReady = {LevelLoader.IsServerLevelReady}");
			Debug.Log($"[NM] NetworkServer.active = {NetworkServer.active}");

			if (!LevelLoader.IsServerLevelReady)
			{
				_pendingConns.Add(conn);
				Debug.Log($"[NM] ⏸️ Queued player join (conn {conn.connectionId}) until LevelReady. Pending count: {_pendingConns.Count}");
				return;
			}

			Debug.Log($"[NM] ✅ Level ready, calling ServerFinalizeAddPlayer for conn {conn.connectionId}");
			ServerFinalizeAddPlayer(conn);
		}

		
		[Server]
		private void ServerFinalizeAddPlayer(NetworkConnectionToClient conn)
		{
			Debug.Log($"[NM] ServerFinalizeAddPlayer for conn {conn.connectionId}");
			
			if (conn.identity == null)
			{
				if (playerPrefab == null)
				{
					Debug.LogError("[NM] Player Prefab puuttuu!");
					return;
				}
				base.OnServerAddPlayer(conn);
			}

			bool isHost = conn.connectionId == 0;
			
			var spawner = SpawnUnitsCoordinator.Instance;
			if (spawner == null)
			{
				Debug.LogError("[NM] SpawnUnitsCoordinator.Instance puuttuu!");
				return;
			}

			// Spawna yksiköt JOKAISELLE pelaajalle (host JA client)
			Debug.Log($"[NM] Spawning units for {(isHost ? "HOST" : "CLIENT")} conn {conn.connectionId}");
			var units = spawner.SpawnPlayersForNetwork(conn, isHost);
			
			if (units != null && units.Length > 0)
			{
				Debug.Log($"[NM] Spawned {units.Length} units for conn {conn.connectionId}");
			}

			// Loput koodista...
			var turnMgr = NetTurnManager.Instance;
			if (turnMgr != null)
				turnMgr.ServerUpdateRequiredCount(NetworkServer.connections.Count);

			if (NetTurnManager.Instance && NetTurnManager.Instance.phase == TurnPhase.Players)
			{
				var pc = conn.identity ? conn.identity.GetComponent<PlayerController>() : null;
				if (pc != null) pc.ServerSetHasEnded(false);
			}

			if (CoopTurnCoordinator.Instance && NetTurnManager.Instance)
			{
				CoopTurnCoordinator.Instance.RpcTurnPhaseChanged(
					NetTurnManager.Instance.phase,
					NetTurnManager.Instance.turnNumber,
					true
				);
			}

			if (GameModeManager.SelectedMode == GameMode.Versus)
			{
				var pc = conn.identity ? conn.identity.GetComponent<PlayerController>() : null;
				if (pc != null && PvPTurnCoordinator.Instance != null)
					PvPTurnCoordinator.Instance.ServerRegisterPlayer(pc);
			}
		}

		[Server]
		public void ServerSpawnEnemies()
		{
			// Pyydä SpawnUnitsCoordinatoria luomaan viholliset
			var enemies = SpawnUnitsCoordinator.Instance.SpawnEnemies();

			// Synkronoi viholliset verkkoon Mirrorin avulla
			foreach (var enemy in enemies)
			{
				if (enemy != null)
				{
					NetworkServer.Spawn(enemy);
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

		public void SetEnemies()
		{ 
			SpawnUnitsCoordinator.Instance.SetEnemiesSpawned(false);

			if (GameModeManager.SelectedMode == GameMode.CoOp)
			{
				ServerSpawnEnemies();
			}
		}		
	}
}
