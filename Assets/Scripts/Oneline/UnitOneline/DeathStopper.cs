using UnityEngine;
using Mirror;

/// <summary>
/// DeathStopper: varma "hätäjarru" kun Unit kuolee kesken liikkeen verkossa.
/// Kutsu serverillä ENNEN FreezeBeforeDespawn():ia.
/// Toiminnot:
/// 1) Pysäyttää MoveActionin deterministisesti (ForceStopNow -> ActionComplete -> UI busy vapautuu).
/// 2) Lähettää ClientRpc-teleportin samaan paikkaan (snäppää ja nollaa interpolaatiopuskurin).
/// 3) Nollaa mahdollisen rigidbodyn nopeudet.
/// 4) (Valinn.) kytkee *minkä tahansa* NetworkTransformin pois/päälle clientilla ilman kovaa tyyppiriippuvuutta.
///    -> Ei tarvitse viitata Mirror.Components -asmdeffiin, joten tämä kääntyy vaikka et lisää sitä.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class DeathStopper : NetworkBehaviour
{
    [Header("Optional component refs (autofill, jos tyhjä)")]
    [SerializeField] private MoveAction moveAction;

    // HUOM: Ei kovaa viittausta NetworkTransform / NetworkAnimator -tyyppeihin.
    // Haetaan ne niminä, jolloin tämä skripti ei vaadi Mirror.Components -asmdef -referenssiä.
    [SerializeField] private Behaviour netTransformBehaviour; // esim. "NetworkTransform"
    [SerializeField] private Behaviour netAnimatorBehaviour;  // esim. "NetworkAnimator"

    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody rb;

    [Header("Behavior")]
    [Tooltip("Kytke NT pois/päälle clientilla snäpin yhteydessä, jotta interpolaatiopuskurit nollautuvat.")]
    [SerializeField] private bool toggleNetworkTransformOnSnap = true;

    void Awake()
    {
        if (!moveAction) moveAction = GetComponent<MoveAction>();

        // Etsi dynaamisesti nimellä -> ei tarvita tyyppiviitettä
        if (!netTransformBehaviour)
            netTransformBehaviour = GetComponent("NetworkTransform") as Behaviour;
        if (!netAnimatorBehaviour)
            netAnimatorBehaviour = GetComponent("NetworkAnimator") as Behaviour;

        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!rb) rb = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// SERVER ONLY. Katkaise liike, snäppää pose kaikille ja valmistele tuho.
    /// Kutsu tämä heti, kun päätät että unit kuolee (ennen komponenttien disablointia).
    /// </summary>
    [Server]
    public void HaltForDeath()
    {
        // 1) Pysäytä liike deterministisesti (ennen kuin actionit disabloidaan)
        try
        {
            if (moveAction)
            {
                moveAction.ForceStopNow(); // kutsuu ActionComplete() -> UI busy vapautuu
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[DeathStopper] ForceStopNow() poikkeus: {ex.Message}");
        }

        // 2) Snäppää nykyiseen paikkaan kaikki clientit (ja nollaa interpolaatiopuskuri)
        var pos = transform.position;
        var rot = transform.rotation;
        RpcTeleportSnap(pos, rot);
    }

    [ClientRpc]
    private void RpcTeleportSnap(Vector3 pos, Quaternion rot)
    {

        try
        {
            // a) Katkaise hetkeksi (mahd.) NetworkTransform (tyhjentää interpolaation)
            bool reenableNT = false;
            if (toggleNetworkTransformOnSnap && netTransformBehaviour && netTransformBehaviour.enabled)
            {
                netTransformBehaviour.enabled = false;
                reenableNT = true;
            }

            // b) Aseta transform suoraan
            transform.SetPositionAndRotation(pos, rot);

            // c) Nollaa mahdollinen fysiikka-liike
            if (rb)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.Sleep();
            }

            // d) "Pingota" animaattori frameen (estää yhden framen juoksuanimaatiojäännöksen)
            if (animator) animator.Update(0f);

            // e) Ota (mahd.) NetworkTransform takaisin käyttöön
            if (reenableNT && netTransformBehaviour)
            {
                netTransformBehaviour.enabled = true;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[DeathStopper] RpcTeleportSnap poikkeus: {ex.Message}");
        }
    }

    /// <summary>
    /// Apumetodi: turvallinen yritys kutsua hätäjarru.
    /// </summary>
    [Server]
    public static void TryHalt(Unit unit)
    {
        if (!unit)
        {
            Debug.Log("[DeathStopper] Unit Is null!");
            return; 
        }
        var ds = unit.GetComponent<DeathStopper>();
        if (ds) ds.HaltForDeath();
    }
}
