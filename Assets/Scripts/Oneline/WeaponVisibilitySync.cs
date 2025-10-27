using Mirror;
using UnityEngine;

public class WeaponVisibilitySync : NetworkBehaviour
{
    [Header("Unit Weapons Refs")]
    [SerializeField] private Transform rifleRightHandTransform;
    [SerializeField] private Transform rifleLeftHandTransform;
    [SerializeField] private Transform meleeLeftHandTransform;
    [SerializeField] private Transform grenadeRightHandTransform;

   
    private NetVisibility rifleRightVis, rifleLeftVis ,meleeLeftVis, grenadeRightVis;

    void Awake()
    {
        if (rifleRightHandTransform) rifleRightVis = rifleRightHandTransform.GetComponent<NetVisibility>();
        if (rifleLeftHandTransform) rifleLeftVis= rifleLeftHandTransform.GetComponent<NetVisibility>();
        if (meleeLeftHandTransform) meleeLeftVis = meleeLeftHandTransform.GetComponent<NetVisibility>();
        if (grenadeRightHandTransform) grenadeRightVis = grenadeRightHandTransform.GetComponent<NetVisibility>();
    }

    // --- OWNER kutsuu tätä (esim. AE:ssä) ---
    public void OwnerRequestSet(bool rifleRight,bool rifleLeft, bool meleeLeft, bool grenade)
    {
        // Offline: suoraan paikalliset
        if (!NetworkClient.active && !NetworkServer.active)
        {
            SetLocal(rifleRight, rifleLeft, meleeLeft, grenade);
            return;
        }

        // Online: vain omistaja saa pyytää
        var ni = GetComponent<NetworkIdentity>();
        if (isClient && ni && ni.isOwned)
        {
            CmdSet(rifleRight, rifleLeft,meleeLeft, grenade);
        }
    }

    [Command(requiresAuthority = true)]
    private void CmdSet(bool rifleRight, bool rifleLeft ,bool meleeLeft, bool grenade)
    {
        // Serverissä voi halutessa käyttää server-authoritatiivista NetVisibilityä:
        // jos käytössä, aseta serverillä -> SyncVar/RPC hoitaa muille
        if (rifleRightVis)   rifleRightVis.ServerSetVisible(rifleRight);
        if (rifleLeftVis)   rifleLeftVis.ServerSetVisible(rifleLeft);
        if (meleeLeftVis) meleeLeftVis.ServerSetVisible(meleeLeft);
        if (grenadeRightVis) grenadeRightVis.ServerSetVisible(grenade);

        // Lisäksi varma ClientRpc (jos NetVisibility ei kata kaikkea):
        RpcSet(rifleRight, rifleLeft ,meleeLeft, grenade);
    }

    [ClientRpc]
    private void RpcSet(bool rifleRight, bool rifleLeft ,bool meleeLeft, bool grenade)
    {
        SetLocal(rifleRight, rifleLeft ,meleeLeft, grenade);
    }

    private void SetLocal(bool rifleRight,bool rifleLeft, bool meleeLeft, bool grenade)
    {
        // Jos sinulla on NetVisibility, käytä sen "pehmeää" piilotusta,
        // muuten pelkkä SetActive/renderer.enabled
        if (rifleRightHandTransform) rifleRightHandTransform.gameObject.SetActive(rifleRight);
        if (rifleLeftHandTransform) rifleLeftHandTransform.gameObject.SetActive(rifleLeft);
        if (meleeLeftHandTransform) meleeLeftHandTransform.gameObject.SetActive(meleeLeft);
        if (grenadeRightHandTransform) grenadeRightHandTransform.gameObject.SetActive(grenade);

        // Esim. renderer-tason piilotus:
        // ToggleRenderers(rifleTransform, rifle);
        // ToggleRenderers(meleeTransform, melee);
        // ToggleRenderers(grenadeTransform, grenade);
    }

    private static void ToggleRenderers(Transform t, bool visible)
    {
        if (!t) return;
        foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;
    }

    [Server]
    public void ServerForceSet(bool rifleRight, bool rifleLeft, bool meleeLeft, bool grenade)
    {
        // Aseta server-authoritatiivinen tila (jos käytät NetVisibilityä)
        if (rifleRightVis)   rifleRightVis.ServerSetVisible(rifleRight);
        if (rifleLeftVis)    rifleLeftVis.ServerSetVisible(rifleLeft);
        if (meleeLeftVis)    meleeLeftVis.ServerSetVisible(meleeLeft);
        if (grenadeRightVis) grenadeRightVis.ServerSetVisible(grenade);

        // Ja lähetä varmuuden vuoksi kaikille klienteille
        RpcSet(rifleRight, rifleLeft, meleeLeft, grenade);
    }
}
