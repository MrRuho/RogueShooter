using UnityEngine;


[DisallowMultipleComponent]
public class CoverVisualizer : MonoBehaviour
{
[Header("Refs")]
    [SerializeField] private PathFinding pathfinding;
    [SerializeField] private LevelGrid levelGrid;
    [SerializeField] private Camera cam;
    [SerializeField] private Material unlitTransparentMat; // Unlit/Transparent tms.

    [Header("Raycast")]
    [SerializeField] private LayerMask groundMask = ~0; // millä layereilla lattia/maa on

    [Header("Style")]
    [SerializeField] private float yOffset = 0.05f;    // nosta vähän lattiasta
    [SerializeField] private float edgeInset = 0.48f;  // 0.45–0.49
    [SerializeField] private float barLen = 0.90f;     // suhteessa cellSizeen
    [SerializeField] private float barWidth = 0.06f;   // X/Z -ohuus
    [SerializeField] private float barHeight = 0.06f;  // Y-paksuus
    [SerializeField] private Color lowColor  = new(0.2f, 1f, 0.2f, 0.55f);
    [SerializeField] private Color highColor = new(0.2f, 0.5f, 1f, 0.80f);

    [Header("Walls (optional)")]
    [SerializeField] private bool showWalls = true;
    [SerializeField] private Color wallColor = new(1f, 0.4f, 0.1f, 0.80f);

    Transform n,e,s,w; MeshRenderer rn,re,rs,rw; float cell;

    void Awake() {
        if (!pathfinding) pathfinding = FindFirstObjectByType<PathFinding>();
        if (!levelGrid)  levelGrid  = LevelGrid.Instance;
        if (!cam) cam = Camera.main;

        cell = levelGrid.GetCellSize();
        (n,rn) = CreateBar("N");
        (e,re) = CreateBar("E");
        (s,rs) = CreateBar("S");
        (w,rw) = CreateBar("W");
        HideAll();
    }

    (Transform, MeshRenderer) CreateBar(string name) {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = $"CoverHover_{name}";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        var mr = go.GetComponent<MeshRenderer>();
        if (unlitTransparentMat) mr.sharedMaterial = unlitTransparentMat;
        go.SetActive(false);
        return (go.transform, mr);
    }

    void Update() {
        BaseAction action = UnitActionSystem.Instance.GetSelectedAction();
        if (action == null) return;
        if (!pathfinding || !levelGrid || !cam || action.GetActionName() != "Move") { HideAll(); return; }

        var ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 500f, groundMask, QueryTriggerInteraction.Collide)) { HideAll(); return; }

        // Ruudukkoon
        var gp = levelGrid.GetGridPosition(hit.point);
        var node = pathfinding.GetNode(gp.x, gp.z, gp.floor);
        if (node == null|| !node.GetIsWalkable()) { HideAll(); return; }

        var c = levelGrid.GetWorldPosition(gp);
        c.y += yOffset;

        // Reunan keskikohdat
        var north = c + new Vector3(0, 0,  cell * edgeInset);
        var south = c + new Vector3(0, 0, -cell * edgeInset);
        var eastP = c + new Vector3( cell * edgeInset, 0, 0);
        var westP = c + new Vector3(-cell * edgeInset, 0, 0);

        // N/S = pituus X-suunnassa, E/W = pituus Z-suunnassa
        DrawBar(node.HasHighCover(CoverMask.N), node.HasLowCover(CoverMask.N), node.HasWall(EdgeMask.N), n, rn, north, new Vector3(cell*barLen, barHeight, barWidth));
        DrawBar(node.HasHighCover(CoverMask.S), node.HasLowCover(CoverMask.S), node.HasWall(EdgeMask.S), s, rs, south, new Vector3(cell*barLen, barHeight, barWidth));
        DrawBar(node.HasHighCover(CoverMask.E), node.HasLowCover(CoverMask.E), node.HasWall(EdgeMask.E), e, re, eastP, new Vector3(barWidth, barHeight, cell*barLen));
        DrawBar(node.HasHighCover(CoverMask.W), node.HasLowCover(CoverMask.W), node.HasWall(EdgeMask.W), w, rw, westP, new Vector3(barWidth, barHeight, cell*barLen));
    }

    void DrawBar(bool high, bool low, bool wall, Transform tr, MeshRenderer mr, Vector3 pos, Vector3 size) {
        if (!high && !low && !(showWalls && wall)) { tr.gameObject.SetActive(false); return; }
        tr.gameObject.SetActive(true);
        tr.position = pos;
        tr.localScale = size;

        // Väri prioriteetilla: seinä > high cover > low cover
        var color = (showWalls && wall) ? wallColor : (high ? highColor : lowColor);
        var m = mr.material; // runtime-instanssi
        m.color = color;
    }

    void HideAll() {
        if (n) n.gameObject.SetActive(false);
        if (e) e.gameObject.SetActive(false);
        if (s) s.gameObject.SetActive(false);
        if (w) w.gameObject.SetActive(false);
    }
}
