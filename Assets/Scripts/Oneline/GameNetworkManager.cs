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

		private readonly List<NetworkConnectionToClient> _pendingConns = new();

		[SerializeField] private int hideJoinCodeAfterConnections = 2; // Host + 1 client
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
			
			// ÄLÄ kutsu SpawnObjects() tässä – NetLevelLoader hoitaa sen!
			
			LevelLoader.LevelReady += OnLevelReady_Server;
			SpawnUnitsCoordinator.Instance.SetEnemiesSpawned(false);

			if (GameModeManager.SelectedMode == GameMode.CoOp)
			{
				ServerSpawnEnemies();
			}
		}

		[ServerCallback]
		public  override void OnDestroy()
		{
			LevelLoader.LevelReady -= OnLevelReady_Server;
		}

		[Server]
		private void OnLevelReady_Server(UnityEngine.SceneManagement.Scene mapScene)
		{
			// Level on nyt varmasti ladattu → tyhjennä jono
        foreach (var c in _pendingConns)
            if (c != null) ServerFinalizeAddPlayer(c);

        _pendingConns.Clear();

        // (Tähän voit halutessasi laittaa vihollis-spawnit, ruudukon rebuildin ja vuoron aloituksen)
        // Example:
        // SpawnUnitsCoordinator.Instance?.ServerSpawnEnemiesForLevel();
        // LevelGrid.Instance?.RebuildOccupancyFromScene();
        // NetTurnManager.Instance?.ServerResetAndBegin();
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
		/*
		public override void OnServerAddPlayer(NetworkConnectionToClient conn)
		{
			
			// Jos Level ei ole vielä valmis, jonota ja palaa
			if (!LevelLoader.IsServerLevelReady)
			{
				_pendingConns.Add(conn);
				Debug.Log($"[NM] Queued player join (conn {conn.connectionId}) until LevelReady.");
				return;
			}
			
			ServerFinalizeAddPlayer(conn);
		}
		*/
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
		Debug.Log($"[NM] >>> ServerFinalizeAddPlayer START for conn {conn.connectionId}");
		
		if (conn.identity == null)
		{
			if (playerPrefab == null)
			{
				Debug.LogError("[NM] ❌ Player Prefab (EmptySquad) puuttuu!");
				return;
			}
			Debug.Log($"[NM] Creating player identity for conn {conn.connectionId}");
			base.OnServerAddPlayer(conn);
			Debug.Log($"[NM] Player identity created: {conn.identity?.name}");
		}
		else
		{
			Debug.Log($"[NM] Player identity already exists: {conn.identity.name}");
		}

		bool isHost = conn.connectionId == 0;
		Debug.Log($"[NM] isHost = {isHost} for conn {conn.connectionId}");

		var spawner = SpawnUnitsCoordinator.Instance;
		if (spawner == null)
		{
			Debug.LogError("[NM] ❌❌❌ SpawnUnitsCoordinator.Instance is NULL!");
			Debug.LogError("[NM] TARKISTA että SpawnUnitsCoordinator GameObject on Level 0 scenessä!");
			
			return;
		}

		Debug.Log($"[NM] ✅ SpawnUnitsCoordinator found, spawning units for conn {conn.connectionId}, isHost={isHost}");
		var units = spawner.SpawnPlayersForNetwork(conn, isHost);
		
		if (units == null)
		{
			Debug.LogError($"[NM] ❌ SpawnPlayersForNetwork returned NULL for conn {conn.connectionId}!");
			return;
		}
		
		if (units.Length == 0)
		{
			Debug.LogError($"[NM] ❌ SpawnPlayersForNetwork returned EMPTY array for conn {conn.connectionId}!");
			return;
		}
		
		Debug.Log($"[NM] SpawnPlayersForNetwork returned {units.Length} units");
		
		foreach (var unit in units)
		{
			if (unit == null)
			{
				Debug.LogError($"[NM] ❌ Unit is null in array for conn {conn.connectionId}!");
				continue;
			}
			Debug.Log($"[NM] 🎮 Spawning player unit '{unit.name}' at {unit.transform.position} for connection {conn.connectionId}");
			NetworkServer.Spawn(unit, conn);
			Debug.Log($"[NM] ✅ Unit '{unit.name}' spawned with netId {unit.GetComponent<NetworkIdentity>()?.netId}");
		}

		Debug.Log($"[NM] <<< ServerFinalizeAddPlayer COMPLETE for conn {conn.connectionId}, spawned {units.Length} units");

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
			else
				Debug.LogWarning("[NM] PvP rekisteröinti epäonnistui: PlayerController tai PvPTurnCoordinator puuttuu.");
		}
	}
/*
		
		[Server]
		private void ServerFinalizeAddPlayer(NetworkConnectionToClient conn)
		{
			// 1) Luo player-identity tälle connectionille (vain jos sitä ei ole jo tehty)
			if (conn.identity == null)
			{
				if (playerPrefab == null)
				{
					Debug.LogError("[NM] Player Prefab (EmptySquad) puuttuu!");
					return;
				}
				base.OnServerAddPlayer(conn); // NetworkServer.AddPlayerForConnection(...)
			}

			// 2) Päätä host vs client
			bool isHost = conn.connectionId == 0;

			// 3) Spawnaa pelaajan unitit ja anna niihin authority (siirron Level-scenelle hoitaa teidän SpawnUnitsCoordinator/SpawnRouter)
			var spawner = SpawnUnitsCoordinator.Instance;
			if (spawner == null)
			{
				Debug.LogError("[NM] SpawnUnitsCoordinator.Instance puuttuu (ei löydy Level-scenestä?).");
				return;
			}

			var units = spawner.SpawnPlayersForNetwork(conn, isHost);
			foreach (var unit in units)
			{
				Debug.Log($"[NM] Spawning player unit {unit.name} for connection {conn.connectionId}, isHost={isHost}");
				NetworkServer.Spawn(unit, conn);
			}

			// 4) Päivitä vuoronhallinnan pelaajamäärä
			var turnMgr = NetTurnManager.Instance;
			if (turnMgr != null)
				turnMgr.ServerUpdateRequiredCount(NetworkServer.connections.Count);

			// 5) Jos nyt on Players-vuoro, avaa UI tälle uudelle pelaajalle
			if (NetTurnManager.Instance && NetTurnManager.Instance.phase == TurnPhase.Players)
			{
				var pc = conn.identity ? conn.identity.GetComponent<PlayerController>() : null;
				if (pc != null) pc.ServerSetHasEnded(false);
			}

			// 6) Coop-UI päivitys
			if (CoopTurnCoordinator.Instance && NetTurnManager.Instance)
			{
				CoopTurnCoordinator.Instance.RpcTurnPhaseChanged(
					NetTurnManager.Instance.phase,
					NetTurnManager.Instance.turnNumber,
					true
				);
			}

			// 7) PvP-rekisteröinti
			if (GameModeManager.SelectedMode == GameMode.Versus)
			{
				var pc = conn.identity ? conn.identity.GetComponent<PlayerController>() : null;
				if (pc != null && PvPTurnCoordinator.Instance != null)
					PvPTurnCoordinator.Instance.ServerRegisterPlayer(pc);
				else
					Debug.LogWarning("[NM] PvP rekisteröinti epäonnistui: PlayerController tai PvPTurnCoordinator puuttuu.");
			}
		}
		
	*/	

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
