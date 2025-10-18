using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
///     This class is responsible for displaying the action button TXT in the UI
/// </summary>

public class UnitActionButtonUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textMeshPro;
    [SerializeField] private Button actionButton;
    [SerializeField] private GameObject actionButtonSelectedVisual;

     // --- UUTTA: kulmabadge
    [Header("Corner badge (optional)")]
    [SerializeField] private RectTransform cornerRoot;     // drag: CornerBadge
    [SerializeField] private TextMeshProUGUI cornerText;   // drag: CornerText

    private BaseAction baseAction;

    public void SetBaseAction(BaseAction baseAction)
    {
        this.baseAction = baseAction;
        textMeshPro.text = baseAction.GetActionName().ToUpper();

        actionButton.onClick.AddListener(() =>
        {
            UnitActionSystem.Instance.SetSelectedAction(baseAction);
        });

        RefreshCorner();
    }
    
    void OnEnable()
    {
        TrySub(true);
        RefreshCorner();
    }

    void OnDisable()
    {
        TrySub(false);
    }

    private void TrySub(bool on)
    {
        // turvalliset unsub/sub -kutsut
        if (UnitActionSystem.Instance != null)
        {
            if (on)
            {
                UnitActionSystem.Instance.OnSelectedUnitChanged   += OnUiRefresh;
                UnitActionSystem.Instance.OnSelectedActionChanged += OnUiRefresh;
                UnitActionSystem.Instance.OnActionStarted         += OnUiRefresh;

                // HUOM: tämä on EventHandler<bool>, EI EventHandler
                UnitActionSystem.Instance.OnBusyChanged           += OnBusyChanged;
            }
            else
            {
                UnitActionSystem.Instance.OnSelectedUnitChanged   -= OnUiRefresh;
                UnitActionSystem.Instance.OnSelectedActionChanged -= OnUiRefresh;
                UnitActionSystem.Instance.OnActionStarted         -= OnUiRefresh;
                UnitActionSystem.Instance.OnBusyChanged           -= OnBusyChanged;
            }
        }

        if (TurnSystem.Instance != null)
        {
            if (on)  TurnSystem.Instance.OnTurnChanged += OnTurnChanged;
            else     TurnSystem.Instance.OnTurnChanged -= OnTurnChanged;
        }

        if (on)
            BaseAction.OnAnyActionStarted += OnAnyActionStarted;
        else
            BaseAction.OnAnyActionStarted -= OnAnyActionStarted;
    }


    private void OnUiRefresh(object sender, EventArgs e) => RefreshCorner();
    private void OnAnyActionStarted(object sender, EventArgs e) => RefreshCorner();
    private void OnTurnChanged(object sender, EventArgs e) => RefreshCorner();
    
    private void OnBusyChanged(object sender, bool isBusy) => RefreshCorner();

    private void RefreshCorner()
    {
        // Näytä kulmalaskuri vain kranaatti-napissa
        bool isGrenade = baseAction is GranadeAction;
        if (!isGrenade)
        {
            if (cornerRoot) cornerRoot.gameObject.SetActive(false);
            return;
        }

        var unit = UnitActionSystem.Instance ? UnitActionSystem.Instance.GetSelectedUnit() : null;
        int pcs = unit ? unit.GetGrenadePCS() : 0;   // Unitilla on GetGrenadePCS()
        if (cornerText) cornerText.text = pcs.ToString();
        if (cornerRoot) cornerRoot.gameObject.SetActive(true);
    }

    public void UpdateSelectedVisual()
    {
        BaseAction selectedbaseAction = UnitActionSystem.Instance.GetSelectedAction();
        actionButtonSelectedVisual.SetActive(selectedbaseAction == baseAction);
    }

}
