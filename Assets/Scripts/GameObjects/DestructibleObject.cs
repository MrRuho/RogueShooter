using System;
using UnityEngine;

public class DestructibleObject : MonoBehaviour
{
    public static event EventHandler OnAnyDestroyed;

    private GridPosition gridPosition;
    [SerializeField] private int health = 3;

 

    // To prevent multiple destruction events
    private bool isDestroyed;

    void Awake()
    {
        isDestroyed = false;
    }

    private void Start()
    {
      gridPosition = LevelGrid.Instance.GetGridPosition(transform.position);
    }

    public GridPosition GetGridPosition()
    {
        return gridPosition;
    }

    public void Damage(int damageAmount)
    {
        if (isDestroyed) return;

        health -= damageAmount;
        if (health <= 0)
        {
            health = 0;

            if (!isDestroyed)
            {
                isDestroyed = true;
                Destroy(gameObject);
                OnAnyDestroyed?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
