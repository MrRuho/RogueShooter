using System;
using Unity.Mathematics;
using UnityEngine;

public class DestructibleObject : MonoBehaviour
{
    public static event EventHandler OnAnyDestroyed;

    private GridPosition gridPosition;
    [SerializeField] private Transform objectDestroyPrefab;
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

    public void Damage(int damageAmount, Vector3 hitPosition)
    {
        if (isDestroyed) return;

        health -= damageAmount;
        if (health <= 0)
        {
            // Biger overkill means more push force
            int overkill = math.abs(health) + 1;
            health = 0;

            if (!isDestroyed)
            {
                isDestroyed = true;
                Transform createDestroyTransform = Instantiate(objectDestroyPrefab, transform.position, Quaternion.identity);
                ApplyPushForceToChildren(createDestroyTransform, 10f * overkill, hitPosition, 10f);
                Destroy(gameObject);
                OnAnyDestroyed?.Invoke(this, EventArgs.Empty);
            }
        }
    }
    
    private void ApplyPushForceToChildren(Transform root, float pushForce, Vector3 pushPosition, float PushRange)
    {
        foreach (Transform child in root)
        {
            if (child.TryGetComponent<Rigidbody>(out Rigidbody childRigidbody))
            {
                childRigidbody.AddExplosionForce(pushForce, pushPosition, PushRange);
            }

            ApplyPushForceToChildren(child, pushForce, pushPosition, PushRange);
        }
    }
}
