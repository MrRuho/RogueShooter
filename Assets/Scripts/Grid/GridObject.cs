using System.Collections.Generic;

// <summary>
// This class represents a grid object in the grid system.
// It contains a list of units that are present in the grid position.
// It also contains a reference to the grid system and the grid position.
// </summary>

/*
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
        if (unit == null) return;
        var list = GetUnitList();
        if (!list.Contains(unit)) list.Add(unit);
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
*/
public class GridObject
{
    private GridSystem<GridObject> gridSystem;
    private GridPosition gridPosition;

    private readonly List<Unit> unitList = new();
    private readonly List<DestructibleObject> destructibleList = new();

    private IInteractable interactable;

    public GridObject(GridSystem<GridObject> gridSystem, GridPosition gridPosition)
    {
        this.gridSystem = gridSystem;
        this.gridPosition = gridPosition;
    }

    public override string ToString()
    {
        unitList.RemoveAll(u => u == null);
        destructibleList.RemoveAll(d => d == null);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(gridPosition.ToString());
        foreach (var u in unitList) sb.AppendLine(u.ToString());
        foreach (var d in destructibleList) sb.AppendLine(d.ToString());
        return sb.ToString();
    }

    // --- Units ---
    public void AddUnit(Unit unit)
    {
        if (!unit) return;
        if (!unitList.Contains(unit)) unitList.Add(unit);
    }

    public void RemoveUnit(Unit unit) => unitList.Remove(unit);

    public List<Unit> GetUnitList()
    {
        unitList.RemoveAll(u => u == null);
        return unitList;
    }

    public bool HasAnyUnit()
    {
        unitList.RemoveAll(u => u == null);
        return unitList.Count > 0;
    }

    public Unit GetUnit()
    {
        unitList.RemoveAll(u => u == null);
        return unitList.Count > 0 ? unitList[0] : null;
    }

    // --- Destructibles ---
    public void AddDestructible(DestructibleObject d)
    {
        if (!d) return;
        if (!destructibleList.Contains(d)) destructibleList.Add(d);
    }

    public void RemoveDestructible(DestructibleObject d) => destructibleList.Remove(d);

    public List<DestructibleObject> GetDestructibleList()
    {
        destructibleList.RemoveAll(d => d == null);
        return destructibleList;
    }

    public bool HasAnyDestructible()
    {
        destructibleList.RemoveAll(d => d == null);
        return destructibleList.Count > 0;
    }

    // --- Interactable (ennallaan) ---
    public IInteractable GetInteractable() => interactable;
    public void SetInteractable(IInteractable i) => interactable = i;
}