using System.Collections.Generic;
using UnityEngine;

public class FloorVisibility : MonoBehaviour
{

    [SerializeField] private bool dynamicFloorPosition;
    [SerializeField] private List<Renderer> ignoreRendererList;
    private HashSet<Renderer> ignoreSet;
    private Renderer[] rendererArray;
    private int floor;
    private bool? lastVisible;           // vältä turhat muutokset
    private Unit unit;                   // jos kohde on Unit tai sen alla
    private bool forceHidden;            // ulkoinen lukko (esim. kuolema)

    private void Awake()
    {
        rendererArray = GetComponentsInChildren<Renderer>(true);
        unit = GetComponentInParent<Unit>(); // tai GetComponent<Unit>() jos scripti istuu suoraan Unitissa

        if (unit != null)
        {
            // reagoi heti piilotukseen/poistoon
            unit.OnHiddenChangedEvent += OnUnitHiddenChanged;
            forceHidden = unit.IsHidden();
        }

        ignoreSet = new HashSet<Renderer>(ignoreRendererList);
    }

    void OnDisable()
    {
        if (unit != null) unit.OnHiddenChangedEvent -= OnUnitHiddenChanged;
    }

    private void Start()
    {
        floor = LevelGrid.Instance.GetFloor(transform.position);
        Recompute();
    }

    private void OnDestroy()
    {
        if (unit != null) unit.OnHiddenChangedEvent -= OnUnitHiddenChanged;
    }

    private void Update()
    {
        if (dynamicFloorPosition)
        {
            floor = LevelGrid.Instance.GetFloor(transform.position);
        }

        Recompute();
    }

    private void Recompute()
    {
        // 1) kamerakorkeuteen perustuva perusnäkyvyys
        float cameraHeight = CameraController.Instance.GetCameraHeight();
        float floorHeightOffset = 2f;
        bool cameraWantsVisible = (cameraHeight > LevelGrid.FLOOR_HEIGHT * floor + floorHeightOffset) || floor == 0;

        // 2) unitin piilotus "lukitsee" näkymättömäksi
        bool visible = cameraWantsVisible && !forceHidden;

        if (lastVisible.HasValue && lastVisible.Value == visible) return; // ei muutosta
        lastVisible = visible;

        ApplyVisible(visible);
    }

    private void ApplyVisible(bool visible)
    {
        foreach (var r in rendererArray)
        {
            if (!r) continue;
            if (ignoreSet.Contains(r)) continue;
            r.enabled = visible;
        }
    }

    // Jos haluat ulkopuolelta pakottaa piiloon (esim. ragdollin spawner tms.)
    public void SetForceHidden(bool hidden)
    {
        forceHidden = hidden;
        Recompute();
    }

    private void OnUnitHiddenChanged(bool hidden)
    {
        forceHidden = hidden;
        Recompute();
    }
    
    public void AddIgnore(Renderer r)
    {
        ignoreRendererList.Add(r);
        ignoreSet.Add(r);
    }
    public void RemoveIgnore(Renderer r)
    {
        ignoreRendererList.Remove(r);
        ignoreSet.Remove(r);
    }
}
