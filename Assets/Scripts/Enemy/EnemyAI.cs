using System;
using System.Collections;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public static EnemyAI Instance { get; private set; }
    private float timer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject); // valinnainen
    }
    
    private void Start()
    {
        if (GameModeManager.SelectedMode == GameMode.SinglePlayer)
        {
            TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
        }
        
    }
    private void Update()
    {
        // Älä tee mitään co-opissa
        if (GameModeManager.SelectedMode != GameMode.SinglePlayer) return;

        if (TurnSystem.Instance.IsPlayerTurn())
        {
            return;
        }

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            TurnSystem.Instance.NextTurn();
        }
    }

    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        timer = 2f;
    }

    // UUSI: AI-vuoro koroutiinina (ei NextTurn-kutsua sisällä!)
    [Mirror.Server]
    public IEnumerator RunEnemyTurnCoroutine()
    {
        yield return new WaitForSeconds(2f);
    }
        
}
