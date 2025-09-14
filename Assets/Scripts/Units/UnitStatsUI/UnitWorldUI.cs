using UnityEngine;
using TMPro;
using System;
using UnityEngine.UI;
using Mirror;

public class UnitWorldUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI actionPointsText;
    [SerializeField] private Unit unit;
    [SerializeField] private Image healthBarImage;
    [SerializeField] private HealthSystem healthSystem;

    [Header("Visibility")]
    [SerializeField] private GameObject actionPointsRoot;

    private NetworkIdentity unitIdentity;

    private void Awake()
    {
        // Hae unitin NetworkIdentity (tai vanhemmalta jos UI on erillisenä childinä)
        unitIdentity = unit ? unit.GetComponent<NetworkIdentity>() : GetComponentInParent<NetworkIdentity>();
    }

    private void Start()
    {
        Unit.OnAnyActionPointsChanged += Unit_OnAnyActionPointsChanged;
        healthSystem.OnDamaged += HealthSystem_OnDamaged;
        UpdateActionPointsText();
        UpdateHealthBar();

        if (GameModeManager.SelectedMode == GameMode.Versus)
        {
            // reagoi heti vuoronvaihtoihin
            PlayerLocalTurnGate.OnCanActChanged += OnCanActChanged;
            // alkuasetus
            OnCanActChanged(PlayerLocalTurnGate.CanAct);
        }
    }

    private void UpdateActionPointsText()
    {
        actionPointsText.text = unit.GetActionPoints().ToString();
    }

    private void Unit_OnAnyActionPointsChanged(object sender, EventArgs e)
    {
        UpdateActionPointsText();
    }

    private void UpdateHealthBar()
    {
        healthBarImage.fillAmount = healthSystem.GetHealthNormalized();
    }

    private void HealthSystem_OnDamaged(object sender, EventArgs e)
    {
        UpdateHealthBar();
    }

    // Only active player units AP are visible.
    private void OnCanActChanged(bool canAct)
    {
        bool unitIsMine;

        if (GameModeManager.SelectedMode == GameMode.Versus)
        {
            unitIsMine = unitIdentity && unitIdentity.isOwned;
        }
        else
        {
            //coop mode all units all same side
            unitIsMine = true;
        }
        
        bool showAp = (canAct && unitIsMine) || (!canAct && !unitIsMine);
        actionPointsRoot.SetActive(showAp);
    }
}
