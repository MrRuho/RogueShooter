using Mirror;
using UnityEngine;

public class BulletProjectile : NetworkBehaviour
{
    [SyncVar] public uint actorUnitNetId;

    [Header("Visual")]
    [SerializeField] private TrailRenderer trailRenderer;
    
    [Header("Impact VFX")]
    [SerializeField] private Transform unitHitVfxPrefab;
    [SerializeField] private Transform environmentHitVfxPrefab;
    
    [Header("Settings")]
    [SerializeField] private float moveSpeed = 200f;
    [SerializeField] private float bulletRadius = 0.05f;
    [SerializeField] private float maxLifetime = 5f;
    [SerializeField] private float maxTravelDistance = 100f;

    [SyncVar] private Vector3 targetPosition;
    [SyncVar] private bool shouldHitUnits;
    
    private float spawnTime;
    private Vector3 startPosition;
    private bool hasHit;
    private Vector3 flyDirection;
    
    private LayerMask unitsLayerMask;
    private LayerMask environmentLayerMask;

    public void Setup(Vector3 targetPosition, bool canHitUnits)
    {
        this.targetPosition = targetPosition;
        this.shouldHitUnits = canHitUnits;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (trailRenderer && !trailRenderer.emitting) 
            trailRenderer.emitting = true;
        
        spawnTime = Time.time;
        startPosition = transform.position;
        flyDirection = (targetPosition - startPosition).normalized; // ← LASKE KERRAN ALUSSA
        
        SetupLayerMasks();
    }

   private void Start()
    {
        spawnTime = Time.time;
        startPosition = transform.position;
        flyDirection = (targetPosition - startPosition).normalized; // ← LASKE KERRAN ALUSSA
        
        SetupLayerMasks();
    }

    private void SetupLayerMasks()
    {
        int unitsLayer = LayerMask.NameToLayer("Units");
        int obstaclesLayer = LayerMask.NameToLayer("Obstacles");
        int narrowObstaclesLayer = LayerMask.NameToLayer("NarrowObstacles"); // ← KORJAA NIMI (poista "Layer")
        int defaultLayer = LayerMask.NameToLayer("Default");
        int floorLayer = LayerMask.NameToLayer("Floor");
        
        unitsLayerMask = 1 << unitsLayer;
        environmentLayerMask = (1 << obstaclesLayer) | (1 << narrowObstaclesLayer) | (1 << defaultLayer) | (1 << floorLayer);
        
        Debug.Log($"[BULLET SETUP] Units: {unitsLayer}, Env layers: Obstacles({obstaclesLayer}), NarrowObstacles({narrowObstaclesLayer}), Default({defaultLayer}), Floor({floorLayer})");
    }

    private void Update()
    {
        if (hasHit) return;

        if (Time.time - spawnTime > maxLifetime)
        {
            Debug.Log($"[BULLET] Tuhottu: maxLifetime");
            DestroyBulletSilently();
            return;
        }

        float traveledDistance = Vector3.Distance(startPosition, transform.position);
        if (traveledDistance > maxTravelDistance)
        {
            Debug.Log($"[BULLET] Tuhottu: maxTravelDistance");
            DestroyBulletSilently();
            return;
        }

        Vector3 startPos = transform.position;
        float moveDistance = moveSpeed * Time.deltaTime;
        
        // ← KÄYTÄ flyDirection, ÄLÄ laske uudelleen!

        // 1. Tarkista ympäristö
        if (Physics.SphereCast(
            startPos, 
            bulletRadius, 
            flyDirection, // ← KÄYTÄ TALLENNETTUA SUUNTAA
            out RaycastHit envHit, 
            moveDistance, 
            environmentLayerMask, 
            QueryTriggerInteraction.Ignore))
        {
            Debug.Log($"[BULLET] OSUI YMPÄRISTÖÖN: {envHit.collider.name}, Layer: {LayerMask.LayerToName(envHit.collider.gameObject.layer)}");
            HandleHit(envHit, false);
            return;
        }

        // 2. Tarkista Unitit
        if (shouldHitUnits)
        {
            if (Physics.SphereCast(
                startPos, 
                bulletRadius, 
                flyDirection, // ← KÄYTÄ TALLENNETTUA SUUNTAA
                out RaycastHit unitHit, 
                moveDistance, 
                unitsLayerMask, 
                QueryTriggerInteraction.Ignore))
            {
                Debug.Log($"[BULLET] OSUI UNITTIIN: {unitHit.collider.name}");
                HandleHit(unitHit, true);
                return;
            }
        }

        // 3. Liikuta eteenpäin SAMAAN SUUNTAAN
        transform.position += flyDirection * moveDistance;
    }




    private void HandleHit(RaycastHit hit, bool isUnit)
    {
        if (hasHit) return;
        hasHit = true;

        Vector3 hitPoint = hit.point;
        Vector3 hitNormal = hit.normal;
        
        Transform vfxPrefab = isUnit ? unitHitVfxPrefab : environmentHitVfxPrefab;

        if (vfxPrefab != null)
        {
            Quaternion rotation = Quaternion.LookRotation(hitNormal);
            
            SpawnRouter.SpawnLocal(
                vfxPrefab.gameObject,
                hitPoint,
                rotation,
                source: transform
            );
        }

        if (isUnit)
        {
            Debug.Log($"Bullet hit unit at {hitPoint}");
        }
        else
        {
            Debug.Log($"Bullet hit environment: {hit.collider.name} at {hitPoint}");
        }

        DestroyBullet(hitPoint);
    }

    private void DestroyBullet(Vector3 finalPosition)
    {
        transform.position = finalPosition;

        if (trailRenderer)
        {
            trailRenderer.transform.SetParent(null);
            trailRenderer.emitting = false;
            Destroy(trailRenderer.gameObject, trailRenderer.time);
        }

        if (isServer) 
            NetworkServer.Destroy(gameObject);
        else 
            Destroy(gameObject);
    }

    private void DestroyBulletSilently()
    {
        if (trailRenderer)
        {
            trailRenderer.transform.SetParent(null);
            trailRenderer.emitting = false;
            Destroy(trailRenderer.gameObject, trailRenderer.time);
        }

        if (isServer) 
            NetworkServer.Destroy(gameObject);
        else 
            Destroy(gameObject);
    }
}
