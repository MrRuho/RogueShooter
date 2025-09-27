using System;
using UnityEngine;

/// <summary>
/// Updates the pathfinding grid when destructible objects are destroyed.
/// </summary>
public class PathFindingUpdate : MonoBehaviour
{
    private void Start()
    {
        DestructibleObject.OnAnyDestroyed += DestructibleObject_OnAnyDestroyed;
    }

    private void DestructibleObject_OnAnyDestroyed(object sender, EventArgs e)
    {
        DestructibleObject destructibleObject = sender as DestructibleObject;
        PathFinding.Instance.SetIsWalkableGridPosition(destructibleObject.GetGridPosition(), true);
    }
}
