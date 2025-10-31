using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This class is responsible for managing all units in the game.
/// It keeps track of all units, friendly units, and enemy units.
/// It listens to unit spawn and death events to update its lists accordingly.
/// Note: This class Script Script Execution Order is set to be executed after UnitActionSystem.cs. High priority.
/// </summary>
public class UnitManager : MonoBehaviour
{
    public static UnitManager Instance { get; private set; }
    private List<Unit> unitList;
    private List<Unit> friendlyUnitList;
    private List<Unit> enemyUnitList;

    private readonly HashSet<Unit> unitSet = new();

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("There's more than one UnitManager! " + transform + " - " + Instance);
            Destroy(gameObject);
            return;
        }
        Instance = this;

        unitList = new List<Unit>();
        friendlyUnitList = new List<Unit>();
        enemyUnitList = new List<Unit>();
    }

    void OnEnable()
    {
        Unit.OnAnyUnitSpawned += Unit_OnAnyUnitSpawned;
        Unit.OnAnyUnitDead += Unit_OnAnyUnitDead;
    }

    void OnDisable()
    {
        Unit.OnAnyUnitSpawned -= Unit_OnAnyUnitSpawned;
        Unit.OnAnyUnitDead -= Unit_OnAnyUnitDead;
    }
    

    private void Unit_OnAnyUnitSpawned(object sender, EventArgs e)
    {   
        // 1) Estä duplikaatit
        Unit unit = sender as Unit;
        if (!unitSet.Add(unit)) return;
        if (!unitList.Contains(unit)) unitList.Add(unit);

        if (GameModeManager.SelectedMode == GameMode.SinglePlayer || GameModeManager.SelectedMode == GameMode.CoOp)
        {

            if (unit.IsEnemy())
            {
                if (!enemyUnitList.Contains(unit)) enemyUnitList.Add(unit);
                unit.Team = Team.Enemy;
            }
            else
            {
                if (!friendlyUnitList.Contains(unit)) friendlyUnitList.Add(unit);
                unit.Team = Team.Player;
            }

        }
        if (GameModeManager.SelectedMode == GameMode.Versus)
        {
            if(NetworkSync.IsOwnerHost(unit.OwnerId))
            {
                friendlyUnitList.Add(unit);
                unit.Team = Team.Player;
            } else
            {
                enemyUnitList.Add(unit);
                unit.Team = Team.Enemy;
            }
        }
    }

    private void Unit_OnAnyUnitDead(object sender, EventArgs e)
    {
        Unit unit = sender as Unit;
        unitSet.Remove(unit);

        // Poista kaikki esiintymät JA siivoa nullit samalla
        unitList.RemoveAll(u => u == null || u == unit);
        friendlyUnitList.RemoveAll(u => u == null || u == unit);
        enemyUnitList.RemoveAll(u => u == null || u == unit);

    }
    
    
    // Yksinkertainen "puhdas" read-API
    public IReadOnlyList<Unit> GetEnemyUnitList()
    {
        enemyUnitList.RemoveAll(u => u == null);
        return enemyUnitList;
    }

    public List<Unit> GetUnitList()
    {
        return unitList;
    }

    public List<Unit> GetFriendlyUnitList()
    {
        return friendlyUnitList;
    }

    public void ClearAllUnitLists()
    {
        unitList.Clear();
        friendlyUnitList.Clear();
        enemyUnitList.Clear();
    }
}
