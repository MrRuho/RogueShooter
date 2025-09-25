using System;
using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    public event EventHandler OnDead;
    public event EventHandler OnDamaged;

    [SerializeField] private int health = 100;
    private int healthMax;

    // To prevent multiple death events
    private bool isDead;

    void Awake()
    {
        healthMax = health;
        isDead = false;
    }

    public void Damage(int damageAmount)
    {
        if (isDead) return;

        health -= damageAmount;
        if (health <= 0)
        {
            health = 0;

            if (!isDead)
            {
                isDead = true;
                Die();
            }
        }

        OnDamaged?.Invoke(this, EventArgs.Empty);
    }

    private void Die()
    {
        OnDead?.Invoke(this, EventArgs.Empty);
    }

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
        healthMax = Mathf.Max(1, max);
        health    = Mathf.Clamp(current, 0, healthMax);
        OnDamaged?.Invoke(this, EventArgs.Empty);
    }
}
