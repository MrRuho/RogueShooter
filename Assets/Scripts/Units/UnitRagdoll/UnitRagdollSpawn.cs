using System;
using UnityEngine;


[RequireComponent(typeof(HealthSystem))]
public class UnitRagdollSpawn : MonoBehaviour
{
    [SerializeField] private Transform ragdollPrefab;
    [SerializeField] private Transform orginalRagdollRootBone;
    public Transform OriginalRagdollRootBone => orginalRagdollRootBone;

    private HealthSystem HealthSystem;

    private void Awake()
    {
        HealthSystem = GetComponent<HealthSystem>();
        HealthSystem.OnDead += HealthSystem_OnDied;
    }

    private void HealthSystem_OnDied(object sender, EventArgs e)
    {   
        var ni = GetComponentInParent<Mirror.NetworkIdentity>();
        uint id = ni ? ni.netId : 0;
        
        NetworkSync.SpawnRagdoll(
            ragdollPrefab.gameObject, 
            transform.position, 
            transform.rotation,
            id, 
            orginalRagdollRootBone);
    } 
}
