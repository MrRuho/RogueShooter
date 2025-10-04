using System.Collections.Generic;
using UnityEngine;

// <summary>
// This class represents a grid object in the grid system.
// It contains a list of units that are present in the grid position.
// It also contains a reference to the grid system and the grid position.
// </summary>
public class GridObject
{
    private GridSystem<GridObject> gridSystem;
    private GridPosition gridPosition;
    private List<Unit> unitList;
    private IInteractable interactable;

    public GridObject(GridSystem<GridObject> gridSystem, GridPosition gridPosition)
    {
        this.gridSystem = gridSystem;
        this.gridPosition = gridPosition;
        unitList = new List<Unit>();
    }

    public override string ToString()
    {
        string unitListString = "";
        foreach (Unit unit in unitList)
        {
            unitListString += unit + "\n";
        }
        return gridPosition.ToString() + "\n" + unitListString;
    }

    public void AddUnit(Unit unit)
    {
        unitList.Add(unit);
    }

    public void RemoveUnit(Unit unit)
    {
        unitList.Remove(unit);
    }

    public List<Unit> GetUnitList()
    {
        unitList.RemoveAll(u => u == null);
        return unitList;
    }

    public bool HasAnyUnit()
    {
        // Poista tuhotut viitteet (Unity-null huomioiden)
        unitList.RemoveAll(u => u == null);
        return unitList.Count > 0;
    }

    public Unit GetUnit()
    {
        /*
        if (HasAnyUnit())
        {
            return unitList[0];
        }
        else
        {
            return null;
        }
        */
        // Siivoa ja palauta ensimmäinen elossa oleva

        for (int i = unitList.Count - 1; i >= 0; i--)
        {
            if (unitList[i] == null) { unitList.RemoveAt(i); continue; }
        }
        return unitList.Count > 0 ? unitList[0] : null;
    }

    public IInteractable GetInteractable()
    {
        return interactable;
    }
    
    public void SetInteractable(IInteractable interactable)
    {
        this.interactable = interactable;
    }
}
