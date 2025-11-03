using UnityEngine;

/// <summary>
///     This class is responsible for displaying the busy UI when the unit action system is busy
/// </summary>
public class UnitActionBusyUI : MonoBehaviour
{
    private void Start()
    {
       // UnitActionSystem.Instance.OnBusyChanged += UnitActionSystem_OnBusyChanged;
        Hide();
    }

    void OnEnable()
    {
        UnitActionSystem.Instance.OnBusyChanged += UnitActionSystem_OnBusyChanged;
    }

    void OnDisable()
    {
        UnitActionSystem.Instance.OnBusyChanged -= UnitActionSystem_OnBusyChanged;
    }

    private void Show()
    {
        Debug.Log("[UnitActionBusyUI] gameObject.SetActive(true);" );
        gameObject.SetActive(true);
    }
    private void Hide()
    {
        Debug.Log("[UnitActionBusyUI] gameObject.SetActive(false);" );
        gameObject.SetActive(false);
    }
    /// <summary>
    ///     This method is called when the unit action system is busy or not busy
    /// </summary>
    private void UnitActionSystem_OnBusyChanged(object sender, bool isBusy)
    {
        if (isBusy)
        {
            Debug.Log("[UnitActionBusyUI] isBusy" +isBusy );
            Show();
        }
        else
        {
            Debug.Log("[UnitActionBusyUI] isBusy" +isBusy );
            Hide();
        } 
    }
}
