using UnityEngine;
using Mirror;
public abstract class BaseAction : NetworkBehaviour
{
    protected Unit unit;
    protected bool isActive;

    protected virtual void Awake()
    {
        unit = GetComponent<Unit>();
    }
}
