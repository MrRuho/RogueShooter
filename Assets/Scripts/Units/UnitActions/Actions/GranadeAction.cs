/*
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GranadeAction : BaseAction
{
    public event EventHandler ThrowGranade;

    public event EventHandler ThrowReady;

    public Vector3 TargetWorld { get; private set; }

    [SerializeField] private Transform grenadeProjectilePrefab;

    private void Update()
    {
        if (!isActive)
        {
            return;
        }
    }

    public override string GetActionName()
    {
        return "Granade";
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        return new EnemyAIAction
        {
            gridPosition = gridPosition,
            actionValue = 0,
        };
    }

    public override List<GridPosition> GetValidGridPositionList()
    {

        List<GridPosition> validGridPositionList = new();

        GridPosition unitGridPosition = unit.GetGridPosition();
        int range = unit.archetype.throwingRange;
        for (int x = -range; x <= range; x++)
        {
            for (int z = -range; z <= range; z++)
            {
                GridPosition offsetGridPosition = new(x, z, 0);
                GridPosition testGridPosition = unitGridPosition + offsetGridPosition;

                // Check if the test grid position is within the valid range
                if (!LevelGrid.Instance.IsValidGridPosition(testGridPosition)) continue;
 
                int cost = SircleCalculator.Sircle(x, z);
                if (cost > 10 * range) continue;

                validGridPositionList.Add(testGridPosition);
            }
        }

        return validGridPositionList;
    }

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        GetUnit().UseGrenade();
        ActionStart(onActionComplete);
        TargetWorld = LevelGrid.Instance.GetWorldPosition(gridPosition);
        StartCoroutine(TurnAndThrow(.5f, TargetWorld));
    }

    private IEnumerator TurnAndThrow(float delay, Vector3 targetWorld)
    {
        // Odotetaan kunnes RotateTowards palaa true
        float waitAfterAligned = 0.1f; // pienen odotuksen verran
        float alignedTime = 0f;
        
        while (true)
        {
            bool aligned = RotateTowards(targetWorld);

            if (aligned)
            {
                alignedTime += Time.deltaTime;
                if (alignedTime >= waitAfterAligned)
                    break; // ollaan kohdistettu ja odotettu tarpeeksi
            }
            else
            {
                alignedTime = 0f; // resetoi jos ei vielä kohdallaan
            }

            yield return null;
        }

        ThrowGranade?.Invoke(this, EventArgs.Empty);
    }

    public void OnGrenadeBehaviourComplete()
    {
        ThrowReady?.Invoke(this, EventArgs.Empty);
        ActionComplete();
    }
}
*/
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GranadeAction : BaseAction
{
    [Header("Preview")]
    [SerializeField] private GrenadeArcPreview arcPreview; // LineRenderer + GrenadeArcPreview
    [SerializeField] private float cellSizeWU = 2f;        // esim. LevelGrid.Instance.CellSize
    [SerializeField] private Transform throwPoint;         // esim. UnitAnimator.rightHandTransform (optional)

    // (Prefabi-viite saa jäädä, mutta tätä tiedostoa ei käytetä spawniin)
    [SerializeField] private Transform grenadeProjectilePrefab;

    public event EventHandler ThrowGranade;
    public event EventHandler ThrowReady;

    public Vector3 TargetWorld { get; private set; }

    private GridPosition? _lastHover;
    private bool _wasActive;  // reunan tunnistus
    private bool _wasSelected;

    protected override void Awake()
    {
        base.Awake();
        // Jos throwPointia ei ole asetettu inspectorissa, yritetään hakea UnitAnimatorilta
        if (throwPoint == null)
        {
            var anim = GetComponentInChildren<UnitAnimator>(true);
            if (anim != null && anim.GetrightHandTransform() != null)
                throwPoint = anim.GetrightHandTransform();
        }

        // Alusta preview'n perusasetukset kerran
        if (arcPreview != null)
        {
            arcPreview.SetOrigin(throwPoint != null ? throwPoint : transform);
            arcPreview.SetCellSize(cellSizeWU);
        }
    }

    private void OnDisable()
    {
        arcPreview.Hide();
        _lastHover = null; 
        _wasActive = false;
    }

    private void Update()
    {
        // 0) Onko tämä action tällä hetkellä valittuna?
        bool isSelected =
            UnitActionSystem.Instance != null &&
            UnitActionSystem.Instance.GetSelectedAction() == this;

        // 1) Reunan tunnistus valituksi tulossa/poistumassa
        if (isSelected && !_wasSelected)
        {
            // hae Core-scenessä oleva preview, jos viite puuttuu
            if (arcPreview == null)
                arcPreview = FindFirstObjectByType<GrenadeArcPreview>(FindObjectsInactive.Exclude);

            // hae heittopiste, jos puuttuu
            if (throwPoint == null)
            {
                var anim = GetComponentInChildren<UnitAnimator>(true);
                if (anim != null && anim.GetrightHandTransform() != null)
                    throwPoint = anim.GetrightHandTransform();
            }

            if (arcPreview != null)
            {
                arcPreview.SetOrigin(throwPoint != null ? throwPoint : transform);
                arcPreview.SetCellSize(cellSizeWU);
            }

            _lastHover = null; // pakota eka päivitys
        }
        else if (!isSelected && _wasSelected)
        {
            arcPreview?.Hide();
            _lastHover = null;
        }
        _wasSelected = isSelected;

        if (!isSelected) return;

        // 2) Päivitä viiva vain, kun hover-ruutu vaihtuu ja ruutu on validi
        GridPosition gp = LevelGrid.Instance.GetGridPosition(MouseWorld.GetMouseWorldPosition());
        if (!LevelGrid.Instance.IsValidGridPosition(gp))
        {
            arcPreview?.Hide();
            _lastHover = null;
            return;
        }

        if (_lastHover.HasValue && _lastHover.Value == gp) return;
        _lastHover = gp;

        // 3) Piirrä kaari
        Vector3 targetW = LevelGrid.Instance.GetWorldPosition(gp);
        arcPreview?.ShowArcTo(targetW);
    }

    public override string GetActionName() => "Granade";

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
        => new EnemyAIAction { gridPosition = gridPosition, actionValue = 0 };

    public override List<GridPosition> GetValidGridPositionList()
    {
        // Sama kuin aiemmin — vaiheessa 2 karsitaan kaaren törmäystarkistuksella
        List<GridPosition> valid = new();
        GridPosition unitGridPosition = unit.GetGridPosition();

        int range = unit.archetype.throwingRange;

        for (int x = -range; x <= range; x++)
        for (int z = -range; z <= range; z++)
        {
            GridPosition testGridPosition = unitGridPosition + new GridPosition(x, z, 0);
            if (!LevelGrid.Instance.IsValidGridPosition(testGridPosition)) continue;

            int cost = SircleCalculator.Sircle(x, z);
            if (cost > 10 * range) continue;

            valid.Add(testGridPosition);
        }
        return valid;
    }

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        GetUnit().UseGrenade();
        ActionStart(onActionComplete);

        TargetWorld = LevelGrid.Instance.GetWorldPosition(gridPosition);

        // Päätös tehty → piilota preview nyt (spawn ja animaatiot tapahtuvat muualla kuten ennen)
        if (arcPreview != null) arcPreview.Hide();

        StartCoroutine(TurnAndThrow(0.5f, TargetWorld));
    }

    private IEnumerator TurnAndThrow(float delay, Vector3 targetWorld)
    {
        float waitAfterAligned = 0.1f;
        float alignedTime = 0f;

        while (true)
        {
            bool aligned = RotateTowards(targetWorld);
            if (aligned)
            {
                alignedTime += Time.deltaTime;
                if (alignedTime >= waitAfterAligned) break;
            }
            else
            {
                alignedTime = 0f;
            }
            yield return null;
        }

        // Täsmälleen sama eventti kuin ennen — UnitAnimator hoitaa näkyvyydet ja AE-triggerin
        ThrowGranade?.Invoke(this, EventArgs.Empty);
    }

    public void OnGrenadeBehaviourComplete()
    {
        ThrowReady?.Invoke(this, EventArgs.Empty);
        ActionComplete();
    }
}
