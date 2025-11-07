using System;
using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    public event EventHandler OnDamaged;
    public event EventHandler OnDying;
    public event EventHandler OnDead;

    [SerializeField] private int health = 100;
    private int healthMax;

    private bool isDying;
    private bool isDead;
    private Vector3 lastHitPosition;
    public Vector3 LastHitPosition => lastHitPosition;

    private int overkill;
    public int Overkill => overkill;

    void Awake()
    {
        healthMax = health;
        isDying = false;
        isDead = false;
    }

    public void Damage(int damageAmount, Vector3 hitPosition)
    {
        if (isDying || isDead) return;

        health -= damageAmount;
        OnDamaged?.Invoke(this, EventArgs.Empty);

        if (health <= 0)
        {
            lastHitPosition = hitPosition;
            overkill = Math.Abs(health) + 1;
            health = 0;
            BeginDying();
        }
    }

    /*
    private void BeginDying()
    {
        if (isDying) return;
        isDying = true;
        UnitActionSystem.Instance.UnlockInput();
        OnDying?.Invoke(this, EventArgs.Empty);
        StartCoroutine(FinalizeDeathNextFrame());
    }
    */
    private void BeginDying()
    {
        if (isDying) return;
        isDying = true;

        var unit = GetComponent<Unit>();
        if (unit != null)
        {
            var allActions = unit.GetComponents<BaseAction>();
            foreach (var action in allActions)
            {
                if (action != null && action.IsActionActive())
                {
                    Debug.Log($"[HealthSystem] Pakkolopetetaan {action.GetType().Name} kuolevan unitin {unit.name} actionista");
                    action.ForceComplete();
                }
            }
        }
        
        UnitActionSystem.Instance?.UnlockInput();
        
        OnDying?.Invoke(this, EventArgs.Empty);
        StartCoroutine(FinalizeDeathNextFrame());
    }

    private System.Collections.IEnumerator FinalizeDeathNextFrame()
    {
        yield return null;
        FinalizeDeath();
    }

    public void FinalizeDeath()
    {
        if (isDead) return;
        isDead = true;
        OnDead?.Invoke(this, EventArgs.Empty);
    }

    public bool IsDying() => isDying;
    public bool IsDead() => isDead;

    public float GetHealthNormalized()
    {
        return (float)health / healthMax;
    }

    public int GetHealth()
    {
        return health;
    }

    public int GetHealthMax()
    {
        return healthMax;
    }

    public void ApplyNetworkHealth(int current, int max)
    {
        if (isDying || isDead) return;
        
        healthMax = Mathf.Max(1, max);
        health = Mathf.Clamp(current, 0, healthMax);
        OnDamaged?.Invoke(this, EventArgs.Empty);
    }
}
