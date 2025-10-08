using System;
using Unity.Mathematics;
using UnityEngine;
using Mirror;
using System.Collections;

public class DestructibleObject : NetworkBehaviour
{
    public static event EventHandler OnAnyDestroyed;

    private GridPosition gridPosition;
    [SerializeField] private Transform objectDestroyPrefab;
    [SerializeField] private int health = 3;

    // To prevent multiple destruction events
    private bool isDestroyed;

    private bool _walkabilitySet;
    void Awake()
    {
        isDestroyed = false;
    }

    private void Start()
    {
        OnAnyDestroyed += DestructibleObject_OnAnyDestroyed;
        gridPosition = LevelGrid.Instance.GetGridPosition(transform.position);
        TryMarkBlocked();
    }

    void OnDisable()
    {
        OnAnyDestroyed -= DestructibleObject_OnAnyDestroyed;
    }
    /// <summary>
    /// Marks the grid position as blocked if not already set.
    /// </summary>
    private void TryMarkBlocked()
    {
        if (_walkabilitySet) return;

        if (PathFinding.Instance != null)
        {
            PathFinding.Instance.SetIsWalkableGridPosition(gridPosition, false);
            _walkabilitySet = true;
        }
        else
        {
            // jos PathFinding käynnistyy myöhemmin (scene-reload + spawn)
            StartCoroutine(DeferBlockOneFrame());
        }
    }

    private IEnumerator DeferBlockOneFrame()
    {
        yield return null; // 1 frame
        if (PathFinding.Instance != null)
        {
            Debug.Log("Later update: Deferring walkability set for destructible object at " + gridPosition);
            PathFinding.Instance.SetIsWalkableGridPosition(gridPosition, false);
            _walkabilitySet = true;
        }
    }

    public GridPosition GetGridPosition()
    {
        return gridPosition;
    }

    public void Damage(int damageAmount, Vector3 hitPosition)
    {
        if (isDestroyed) return;

        health -= damageAmount;
        if (health > 0) return;

        int overkill = math.abs(health) + 1;
        health = 0;
        isDestroyed = true;

        if (isServer)
        {
            RpcPlayDestroyFx(hitPosition, overkill);
            RpcSetSoftHidden(true);
            StartCoroutine(DestroyAfter(0.30f));
            return;
        }

        // Offline (ei serveriä eikä clienttia)
        if (!NetworkClient.active && !NetworkServer.active)
        {
            PlayDestroyFx(hitPosition, overkill);
            SetSoftHiddenLocal(true);
            StartCoroutine(DestroyAfter(0.30f));
        }
    }

    private void PlayDestroyFx(Vector3 hitPosition, int overkill)
    {
        var t = Instantiate(objectDestroyPrefab, transform.position, Quaternion.identity);
        ApplyPushForceToChildren(t, 10f * overkill, hitPosition, 10f);
        OnAnyDestroyed?.Invoke(this, EventArgs.Empty);
    }

    [ClientRpc]
    private void RpcPlayDestroyFx(Vector3 hitPosition, int overkill)
    {
        // Clientit: toista sama paikallisesti
        PlayDestroyFx(hitPosition, overkill);
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

    private IEnumerator DestroyAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (isServer) NetworkServer.Destroy(gameObject);
        else Destroy(gameObject);
        OnAnyDestroyed?.Invoke(this, EventArgs.Empty);
        
    }

    [ClientRpc]
    private void RpcSetSoftHidden(bool hidden)
    {
        SetSoftHiddenLocal(hidden);
    }

    private void SetSoftHiddenLocal(bool hidden)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = !hidden;

        foreach (var c in GetComponentsInChildren<Collider>(true))
            c.enabled = !hidden;
    }
    
    private void DestructibleObject_OnAnyDestroyed(object sender, EventArgs e)
    {
        EdgeBaker.Instance.RebakeEdgesAround(gridPosition);
    }
    
}
