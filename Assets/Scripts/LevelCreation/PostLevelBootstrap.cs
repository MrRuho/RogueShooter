using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
///  Tämä toistaiseksi auttaa vain Solopelissä alussa lataamaan pelaajan yksiköt.
/// </summary>
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
        if (NetMode.IsRemoteClient) return;
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

        // OFFLINE: (jos haluat tukea SP-tilan bootstrapin tässä)
        if (!NetworkClient.active && !NetworkServer.active)
        {
            // Kutsu suoraan omaa SP-metodiasi, esim:
            spawner.SpwanSinglePlayerUnits();
            LevelGrid.Instance?.RebuildOccupancyFromScene();
            TurnSystem.Instance?.ResetAndBegin();
        }
    }
}
