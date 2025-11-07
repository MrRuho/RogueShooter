using UnityEngine;
using System;
using Mirror;
using System.Collections;

[RequireComponent(typeof(MoveAction))]
public class UnitAnimator : NetworkBehaviour
{

    [Header("UnitWeaponVisibilitySync")]
    [SerializeField] private WeaponVisibilitySync weaponVis;

    [Header("Animators")]
    [SerializeField] private Animator animator;
    [SerializeField] private NetworkAnimator netAnim;

    [Header("Projectiles")]
    [SerializeField] private GameObject bulletProjectilePrefab;
    [SerializeField] private GameObject granadeProjectilePrefab;

    [Header("Spawnpoints")]
    [SerializeField] private Transform shootPointTransform;
    [SerializeField] private Transform rightHandTransform;

    [Header("Visual Effects")]
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private float muzzleFlashDuration = 0.1f;

    [Header("Audio")]
    [SerializeField] private AudioSource weaponAudioSource;
    [SerializeField] private AudioSource tailAudioSource; // ← LISÄÄ TÄMÄ
    [SerializeField] private AudioClip[] rifleShootVariations;
    [SerializeField] private AudioClip rifleShootTail; // ← Muuta nimi selkeämmäksi
    
    [Header("Audio Settings")]
    [SerializeField] private float pitchVariation = 0.1f;
    [SerializeField] private float volumeVariation = 0.15f;
    [SerializeField] private float baseVolume = 1f;
    [SerializeField] private float maxHearingDistance = 50f;
    [SerializeField] private AnimationCurve volumeRolloff = AnimationCurve.Linear(0, 1, 1, 0);

    private static bool IsNetworkActive() => NetworkClient.active || NetworkServer.active;

    private MoveAction _move;
    private ShootAction _shoot;
    private GranadeAction _grenade;
    private MeleeAction _melee;

    private bool useNetwork;

  
    private HealthSystem hs;

    private int currentShotInBurst = 0;
    private int totalShotsInBurst = 0;

    private void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!netAnim) netAnim = GetComponent<NetworkAnimator>();

        useNetwork = NetMode.IsOnline
             && netAnim != null
             && (isServer || isOwned);

        TryGetComponent(out _move);
        TryGetComponent(out _shoot);
        TryGetComponent(out _grenade);
        TryGetComponent(out _melee);
        TryGetComponent(out hs);

        SetupAudioSource();
    }

    private void SetupAudioSource()
    {
        if (weaponAudioSource == null) return;
        
        weaponAudioSource.spatialBlend = 1f;
        weaponAudioSource.rolloffMode = AudioRolloffMode.Custom;
        weaponAudioSource.maxDistance = maxHearingDistance;
        weaponAudioSource.minDistance = 1f;
        weaponAudioSource.dopplerLevel = 0f;
        weaponAudioSource.spread = 0f;
        weaponAudioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, volumeRolloff);
        
        // Aseta tail AudioSource samoilla asetuksilla
        if (tailAudioSource != null)
        {
            tailAudioSource.spatialBlend = 1f;
            tailAudioSource.rolloffMode = AudioRolloffMode.Custom;
            tailAudioSource.maxDistance = maxHearingDistance;
            tailAudioSource.minDistance = 1f;
            tailAudioSource.dopplerLevel = 0f;
            tailAudioSource.spread = 0f;
            tailAudioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, volumeRolloff);
        }
    }

    private void OnEnable()
    {
        if (_move)
        {
            _move.OnStartMoving -= MoveAction_OnStartMoving;
            _move.OnStopMoving -= MoveAction_OnStopMoving;
            _move.OnStartMoving += MoveAction_OnStartMoving;
            _move.OnStopMoving += MoveAction_OnStopMoving;
        }

        if (_shoot)
        {
            _shoot.OnShoot -= ShootAction_OnShoot;
            _shoot.OnShoot += ShootAction_OnShoot;
        }

        if (_grenade)
        {
            _grenade.ThrowGranade -= GrenadeAction_ThrowGranade;
            _grenade.ThrowReady -= GrenadeAction_ThrowReady;
            _grenade.ThrowGranade += GrenadeAction_ThrowGranade;
            _grenade.ThrowReady += GrenadeAction_ThrowReady;
        }

        if (_melee)
        {
            _melee.OnMeleeActionStarted -= MeleeAction_OnMeleeActionStarted;
            _melee.OnMeleeActionCompleted -= MeleeAction_OnMeleeActionCompleted;
            _melee.OnMeleeActionStarted += MeleeAction_OnMeleeActionStarted;
            _melee.OnMeleeActionCompleted += MeleeAction_OnMeleeActionCompleted;
        }

        if (hs != null)
            hs.OnDying += OnDying_StopSending;
    }
    
    private void OnDisable()
    {
        if (_move)
        {
            _move.OnStartMoving -= MoveAction_OnStartMoving;
            _move.OnStopMoving  -= MoveAction_OnStopMoving;
        }
        if (_shoot)
        {
            _shoot.OnShoot -= ShootAction_OnShoot;
        }
        if (_grenade)
        {
            _grenade.ThrowGranade -= GrenadeAction_ThrowGranade;
            _grenade.ThrowReady   -= GrenadeAction_ThrowReady;
        }
        if (_melee)
        {
            _melee.OnMeleeActionStarted -= MeleeAction_OnMeleeActionStarted;
            _melee.OnMeleeActionCompleted -= MeleeAction_OnMeleeActionCompleted;
        }

        if (hs != null)
            hs.OnDying -= OnDying_StopSending;
    }

    private void Start()
    {
        EquipRifle();
    }

    public void SetTrigger(string name)
    {
        if (useNetwork) netAnim.SetTrigger(name);
        else animator.SetTrigger(name);
    }
    
    private void OnDying_StopSending(object s, EventArgs e)
    {
        pendingGrenadeAction = null;
        useNetwork = false;
    }

    private void MoveAction_OnStartMoving(object sender, EventArgs e)
    {
        if (hs && (hs.IsDying() || hs.IsDead())) return;
        animator.SetBool("IsRunning", true);
    }
    private void MoveAction_OnStopMoving(object sender, EventArgs e)
    {
        if (hs && (hs.IsDying() || hs.IsDead())) return;
        animator.SetBool("IsRunning", false);
    }

    public Transform ShootPoint => shootPointTransform;
    public GameObject BulletPrefab => bulletProjectilePrefab;

    public void NotifyBurstStart(int burstSize)
    {
        currentShotInBurst = 0;
        totalShotsInBurst = burstSize;
    }

    public void PlayRifleShootEffects()
    {
        if (hs && (hs.IsDying() || hs.IsDead())) return;
        if (weaponAudioSource == null) return;

        currentShotInBurst++;

        // MUZZLE FLASH
        if (muzzleFlashPrefab != null && shootPointTransform != null)
        {
            GameObject flash = Instantiate(muzzleFlashPrefab, shootPointTransform.position, shootPointTransform.rotation);
            Destroy(flash, muzzleFlashDuration);
        }

        // LAUKAISUÄÄNI
        if (rifleShootVariations != null && rifleShootVariations.Length > 0)
        {
            AudioClip shotClip = rifleShootVariations[UnityEngine.Random.Range(0, rifleShootVariations.Length)];
            
            float pitch = 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
            float volume = baseVolume + UnityEngine.Random.Range(-volumeVariation, volumeVariation);

            weaponAudioSource.pitch = pitch;
            weaponAudioSource.PlayOneShot(shotClip, volume);
            weaponAudioSource.pitch = 1f;
        }

        // TAIL-ÄÄNI (erillisellä AudioSourcella, soitetaan samanaikaisesti tai pienellä viiveellä)
        if (tailAudioSource != null && rifleShootTail != null)
        {
            float tailPitch = 1f + UnityEngine.Random.Range(-pitchVariation * 0.3f, pitchVariation * 0.3f);
            float tailVolume = baseVolume * 0.7f + UnityEngine.Random.Range(-volumeVariation * 0.5f, volumeVariation * 0.5f);
            
            tailAudioSource.pitch = tailPitch;
            tailAudioSource.PlayOneShot(rifleShootTail, tailVolume);
            tailAudioSource.pitch = 1f;
        }
    }

    private void ShootAction_OnShoot(object sender, ShootAction.OnShootEventArgs e)
    {
        if (hs && (hs.IsDying() || hs.IsDead())) return;

        if (e.targetUnit == null)
        {
            return;
        }

        SetTrigger("Shoot");
        PlayRifleShootEffects();

        Vector3 target = BulletTargetCalculator.CalculateBulletTarget(
            e.targetUnit,
            e.shotTier,
            e.shootingUnit
        );

        // Määritä pitikö osua Unittiin
        bool shouldHitUnit = e.shotTier != ShotTier.CritMiss && e.shotTier != ShotTier.Close;

        if (NetMode.IsOnline)
        {
            NetworkSync.SpawnBullet(bulletProjectilePrefab, shootPointTransform.position, target, shouldHitUnit, this.GetActorId());
        }
        else
        {
            OfflineGameSimulator.SpawnBullet(bulletProjectilePrefab, shootPointTransform.position, target, shouldHitUnit);
        }
    }


    private void MeleeAction_OnMeleeActionStarted(object sender, EventArgs e)
    {
        if (hs && (hs.IsDying() || hs.IsDead())) return;
        EquipMelee();
        SetTrigger("Melee");
    }
    
    private void MeleeAction_OnMeleeActionCompleted(object sender, EventArgs e)
    {
        if (hs && (hs.IsDying() || hs.IsDead())) return;
        EquipRifle();
    }

    private void GranadeActionStart()
    {
        if (hs && (hs.IsDying() || hs.IsDead())) return;
        weaponVis.OwnerRequestSet(rifleRight: false, rifleLeft: true, meleeLeft: false, grenade: false);
    }
    
    private Vector3 pendingGrenadeTarget;
    private GranadeAction pendingGrenadeAction; 
    
    private void GrenadeAction_ThrowGranade(object sender, EventArgs e)
    {
        if (hs && (hs.IsDying() || hs.IsDead())) return;
        pendingGrenadeAction = (GranadeAction)sender;
        pendingGrenadeTarget = pendingGrenadeAction.TargetWorld;

        GranadeActionStart();
        SetTrigger("ThrowGrenade");
   
    }

    public void AE_PickGrenadeStand()
    {
        if (hs && (hs.IsDying() || hs.IsDead())) return;
        EguipGranade();
    }

    public Transform ThrowPoint => rightHandTransform;
    public GameObject GrenadePrefab => granadeProjectilePrefab;

    public void AE_ThrowGrenadeStandRelease()
    {
        if (hs && (hs.IsDying() || hs.IsDead())) return;
        if (pendingGrenadeAction == null) return;

        if (NetworkClient.active || NetworkServer.active)
        {
            var ni = GetComponentInParent<NetworkIdentity>();
            if (!(isClient && ni && ni.isOwned)) return;
        }

        Vector3 origin = rightHandTransform.position;

        float farWU = pendingGrenadeAction.GetMaxThrowRangeWU();

        if (NetMode.IsOnline)

            NetworkSync.SpawnGrenade(granadeProjectilePrefab, origin, pendingGrenadeTarget, farWU, this.GetActorId());

        else
        {
            OfflineGameSimulator.SpawnGrenade(granadeProjectilePrefab, origin, pendingGrenadeTarget, farWU);
        }

        pendingGrenadeAction?.OnGrenadeBehaviourComplete();
        pendingGrenadeAction = null;
    }
    
    public void AE_OnGrenadeThrowStandFinished()
    {
        if (hs && (hs.IsDying() || hs.IsDead())) return;
        EquipRifle();
    }

    private void GrenadeAction_ThrowReady(object sender, EventArgs e)
    {
       weaponVis.OwnerRequestSet(rifleRight: false, rifleLeft: true, meleeLeft: false, grenade: false);
    }
     
    private void EquipRifle()
    {
        if (hs && (hs.IsDying() || hs.IsDead())) return;
        weaponVis.OwnerRequestSet(rifleRight: true, rifleLeft: false, meleeLeft: false, grenade: false);
    }
    private void EquipMelee()
    {
        if (hs && (hs.IsDying() || hs.IsDead())) return;
        weaponVis.OwnerRequestSet(rifleRight: true, rifleLeft: false, meleeLeft: true, grenade: false);
    }
    private void EguipGranade()
    {
        if (hs && (hs.IsDying() || hs.IsDead())) return;
        weaponVis.OwnerRequestSet(rifleRight: false, rifleLeft: true, meleeLeft: false, grenade: true);
    }

    public Transform GetrightHandTransform()
    {
        return rightHandTransform;
    }
}
