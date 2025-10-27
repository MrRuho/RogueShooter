using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LoSBlocker : MonoBehaviour
{
    public enum Mode { Manual, AutoFromBounds }

    [Header("Mode")]
    [SerializeField] private Mode mode = Mode.AutoFromBounds;

    [Tooltip("Jos Manual: true = blokkaa LoS:n")]
    [SerializeField] private bool blocksLineOfSight = true;

    [Header("AutoFromBounds-asetukset")]
    [Tooltip("Yli tämän y-kynnyksen (ruudun pohjasta) tulkitaan 'korkeaksi'")]
    [SerializeField] private float heightThresholdY = 1.7f;

    [Tooltip("Käytä näitä kollidereita. Tyhjä = hae automaattisesti lapsista.")]
    [SerializeField] private List<Collider> colliders = new();

    // Seurataan mitä ruutuja tämä tällä hetkellä kattaa
    private readonly HashSet<GridPosition> _covered = new();
    private bool _registered;
    private LevelGrid _lg;

    void OnEnable()
    {
        _lg = LevelGrid.Instance;
        if (colliders.Count == 0)
            colliders.AddRange(GetComponentsInChildren<Collider>());

        Rebuild();  // laske ruudut + blokkaako
        Register(); // vie registryyn
    }

    void OnDisable()
    {
        Unregister();
        colliders.Clear();
        _covered.Clear();
    }

    /// <summary>
    /// Kutsu tätä kun objektin korkeus/muoto muuttuu (esim. tuhoutuessa).
    /// </summary>
    public void Rebuild()
    {
        if (_lg == null) return;

        // 1) Laske yhdistetty bounds (vain colliderit jotka ovat aktiivisia)
        var haveAny = false;
        var bounds = new Bounds(transform.position, Vector3.zero);
        foreach (var c in colliders)
        {
            if (c == null || !c.enabled) continue;
            if (!haveAny) { bounds = c.bounds; haveAny = true; }
            else          { bounds.Encapsulate(c.bounds); }
        }
        if (!haveAny)
        {
            blocksLineOfSight = false;    // ei kollidereita → ei blokkaa
            RefreshCoveredTiles(bounds, hasBounds:false);
            return;
        }

        // 2) Määritä blokkaako: Manual vs Auto
        if (mode == Mode.AutoFromBounds)
        {
            // Ruudun pohjataso: mittaamme korkeuden paikallisesti jokaisessa ruudussa,
            // mutta yksinkertaisuuden vuoksi arvioi koko objektin "max korkeus"
            float maxTop = bounds.max.y;

            // Arvioi peruslattia käyttämällä bounds.min ja LevelGridiä
            var gpMin = _lg.GetGridPosition(bounds.min);
            var basePos = _lg.GetWorldPosition(gpMin);
            float baseY = basePos.y;

            float topAboveBase = maxTop - baseY;
            blocksLineOfSight = topAboveBase >= heightThresholdY;
        }
        // Manual: blocksLineOfSight pysyy sellaisenaan

        // 3) Päivitä mitkä ruudut tämä kattaa
        RefreshCoveredTiles(bounds, hasBounds:true);
    }

    /// <summary>
    /// Rekisteröi/poistaa rekisteristä nykyiset ruudut.
    /// </summary>
    private void Register()
    {
        if (_registered || !blocksLineOfSight || _covered.Count == 0) return;
        LoSBlockerRegistry.AddTiles(_covered);
        _registered = true;
    }

    private void Unregister()
    {
        if (!_registered) return;
        LoSBlockerRegistry.RemoveTiles(_covered);
        _registered = false;
    }

    /// <summary>
    /// Päivittää _covered-ruudut boundsien perusteella ja synkkaa registryyn.
    /// </summary>
    private void RefreshCoveredTiles(Bounds worldBounds, bool hasBounds)
    {
        // Poista vanhat ruudut registryltä
        Unregister();
        _covered.Clear();

        if (!hasBounds || _lg == null || !blocksLineOfSight) return;

        // Muunna bounds → grid-alue
        var min = _lg.GetGridPosition(worldBounds.min);
        var max = _lg.GetGridPosition(worldBounds.max);

        // Jos objekti voi levitä usealle floorille, tässä voi rajata/vahvistaa.
        int floor = min.floor;

        for (int x = Mathf.Min(min.x, max.x); x <= Mathf.Max(min.x, max.x); x++)
        for (int z = Mathf.Min(min.z, max.z); z <= Mathf.Max(min.z, max.z); z++)
        {
            var gp = new GridPosition(x, z, floor);
            if (_lg.IsValidGridPosition(gp))
                _covered.Add(gp);
        }

        // Rekisteröi päivitetty setti
        Register();
    }
}
