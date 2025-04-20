using System;
using UnityEngine;
using UnityEngine.UI;


public class UnitActionSystemUI : MonoBehaviour
{

    [SerializeField] private Transform actionButtonPrefab;
    [SerializeField] private Transform actionButtonContainerTransform;
    private void Start()
    {
        UnitActionSystem.Instance.OnSelectedUnitChanged += UnitActionSystem_OnSelectedUnitChanged;
    }
    private void CreateUnitActionButtons()
    {
       
        Unit selectedUnit = UnitActionSystem.Instance.GetSelectedUnit();
        if (selectedUnit == null)
        {
            Debug.Log("No selected unit found.");
            return;
        }

        foreach (BaseAction baseAction in selectedUnit.GetBaseActionsArray())
        {
            Transform actionButtonTransform = Instantiate( actionButtonPrefab, actionButtonContainerTransform);
            UnitActionButtonUI actionButtonUI = actionButtonTransform.GetComponent<UnitActionButtonUI>();
            actionButtonUI.SetBaseAction(baseAction);

        }
    }

    private void DestroyActionButtons()
    {
        foreach (Transform child in actionButtonContainerTransform)
        {
            Destroy(child.gameObject);
        }
    }

    private void UnitActionSystem_OnSelectedUnitChanged(object sender, EventArgs e)
    {
        DestroyActionButtons();
        CreateUnitActionButtons();
    }
}
