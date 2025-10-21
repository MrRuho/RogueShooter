
using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PostLevelBootstrap : MonoBehaviour
{
    
    private void OnEnable()
    {
        LevelLoader.LevelReady += OnLevelReady;
    }

    private void OnDisable()
    {
        LevelLoader.LevelReady -= OnLevelReady;
    }

    private void OnLevelReady(Scene mapScene)
    {
        // Aja bootstrap aina yhdestä paikasta:
        StartCoroutine(Co_BootstrapAfterLevelReady(mapScene));
    }
    
    private IEnumerator Co_BootstrapAfterLevelReady(Scene mapScene)
    {
        // Odota 1 frame että Level-skenen Start/OnStartServer ehtivät
        yield return null;

        var spawner = FindFirstObjectByType<SpawnUnitsCoordinator>(FindObjectsInactive.Include);
        if (!spawner)
        {
            Debug.LogError("[Bootstrap] SpawnUnitsCoordinator not found in Level scene.");
            yield break;
        }

        if (NetworkServer.active)
        {
            // --- Pelaajien unitit kaikille nykyisille conn:eille ---
            foreach (var kv in NetworkServer.connections)
            {
                var conn = kv.Value;
                if (conn == null) continue;

                // isHost => connectionId == 0
                bool isHost = conn.connectionId == 0;

                // 1) tee unittien prefab-instanssit
                var units = spawner.SpawnPlayersForNetwork(conn, isHost);
                if (units == null) continue;

                // 2) varmistus: siirrä unitit map-scenelle & verkkoon
                foreach (var unit in units)
                {
                    if (!unit) continue;

                    if (unit.scene != mapScene)
                        SceneManager.MoveGameObjectToScene(unit, mapScene);

                    NetworkServer.Spawn(unit, conn);
                }
            }

            // --- Viholliset / kenttäkohtaiset spawniit ---
            // kutsu suoraan sitä metodia, jota oikeasti käytät (esim. ServerSpawnEnemiesForLevel):
            // spawner.ServerSpawnEnemiesForLevel();
            // tai
            // spawner.ServerSpawnEnemies();

            // Ruudukko ajan tasalle
            LevelGrid.Instance?.RebuildOccupancyFromScene();

            // Vuorologiikka alkuun
            NetTurnManager.Instance?.ServerResetAndBegin();
            yield break;
        }

        // OFFLINE: (jos haluat tukea SP-tilan bootstrapin tässä)
        if (!NetworkClient.active && !NetworkServer.active)
        {
            // Kutsu suoraan omaa SP-metodiasi, esim:
            // spawner.SpawnSinglePlayerUnits();
            LevelGrid.Instance?.RebuildOccupancyFromScene();
            TurnSystem.Instance?.ResetAndBegin();
        }
    }
}
