using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using GameKit.Dependencies.Utilities;

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

    public void AttachToPlayer(Transform player)
    {
        transform.SetParent(player, false);
        transform.localPosition = Vector3.zero;
        capsuleCollider.isTrigger = true;
        transform.localScale = Vector3.one * 0.5f;   
    }

    public void DropFromPlayer()
    {

    }
}
