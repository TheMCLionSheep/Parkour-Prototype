using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using FishNet.Connection;

public class Flag : NetworkBehaviour
{
    [SerializeField] private bool teamColor;

    CapsuleCollider capsuleCollider;

    void Awake()
    {
        capsuleCollider = GetComponent<CapsuleCollider>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    [ServerRpc(RequireOwnership = false)]
    public void AttachToPlayerServer(GameObject player, NetworkConnection conn = null)
    {
        // If the flag is not owned by a player yet
        if (base.HasAuthority)
        {
            this.GiveOwnership(conn);
            AttachToPlayerObserver(player);
        }
        else
        {
            Debug.Log("Ownership failed");
            //Debug.Log(base.Owner);
        }
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void AttachToPlayerObserver(GameObject player)
    {
        AttachToPlayer(player);
    }

    public void AttachToPlayer(GameObject player)
    {
        transform.SetParent(player.transform, false);
        transform.localPosition = Vector3.zero;
        capsuleCollider.isTrigger = true;
        transform.localScale = Vector3.one * 0.5f;   
    }

    public void DropFromPlayer()
    {

    }
}
