using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using FishNet.Connection;
using System.Linq;

public class Flag : NetworkBehaviour
{
    [SerializeField] private bool teamColor;
    
    [SerializeField] private float groundDist = 0.01f;
    [SerializeField] private float gravity = -25;
    [SerializeField] private float respawnHeight = 5f;

    private float verticalVelocity = 0f;
    CapsuleCollider capsuleCollider;

    void Awake()
    {
        capsuleCollider = GetComponent<CapsuleCollider>();
    }

    void FixedUpdate()
    {
        bool falling = !CheckGrounded(out RaycastHit groundhit);
        if (falling)
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
        else
        {
            verticalVelocity = 0;
        }

        MoveDown();
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
        }
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void AttachToPlayerObserver(GameObject player)
    {
        AttachToPlayer(player);
    }

    public void AttachToPlayer(GameObject player)
    {
        PlayerTackle playerTackle = player.GetComponent<PlayerTackle>();
        playerTackle.OwnFlag(this);

        transform.SetParent(playerTackle.GetFlagHolder().transform, false);
        transform.localPosition = Vector3.zero;
        capsuleCollider.isTrigger = true;
        transform.localScale = Vector3.one * 0.5f;
    }

    [ServerRpc(RequireOwnership = true)]
    public void DropFromPlayerServer(Vector3 dropPosition, NetworkConnection conn = null)
    {
        this.RemoveOwnership();
        DropFromPlayerObserver(dropPosition);
    }

    [ObserversRpc]
    private void DropFromPlayerObserver(Vector3 dropPosition)
    {
        DropFromPlayer(dropPosition);
    }

    public void DropFromPlayer(Vector3 dropPosition)
    {
        transform.SetParent(null);
        transform.position = dropPosition + Vector3.up * respawnHeight;
        capsuleCollider.isTrigger = false;
        transform.localScale = Vector3.one;   
    }

    private void MoveDown()
    {
        if (verticalVelocity == 0) return;

        // If moving down would hit the ground, snap to ground
        if (CastSelf(transform.position, transform.rotation, Vector3.down, verticalVelocity * Time.deltaTime, out RaycastHit groundHit))
        {
            transform.position += Vector3.down * groundHit.distance;
        }
        else // Else move down by velocity
        {
            transform.position += Vector3.down * -verticalVelocity * Time.deltaTime;
        }
    }

    private bool CheckGrounded(out RaycastHit groundHit)
    {
        bool onGround = CastSelf(transform.position, transform.rotation, Vector3.down, groundDist, out groundHit);
        return onGround;
    }

    public bool CastSelf(Vector3 pos, Quaternion rot, Vector3 dir, float dist, out RaycastHit hit)
    {
        // Get Parameters associated with the KCC
        Vector3 center = rot * capsuleCollider.center + pos;

        Debug.DrawRay(center, transform.forward, Color.yellow);

        float radius = capsuleCollider.radius;
        float height = capsuleCollider.height;

        // Get top and bottom points of collider
        Vector3 bottom = center + rot * Vector3.down * (height / 2 - radius);
        Vector3 top = center + rot * Vector3.up * (height / 2 - radius);

        // Check what objects this collider will hit when cast with this configuration excluding itself
        IEnumerable<RaycastHit> hits = Physics.CapsuleCastAll(
            top, bottom, radius, dir, dist, ~0, QueryTriggerInteraction.Ignore)
            .Where(hit => hit.collider.transform != transform);
        bool didHit = hits.Count() > 0;

        // Find the closest objects hit
        float closestDist = didHit ? Enumerable.Min(hits.Select(hit => hit.distance)) : 0;
        IEnumerable<RaycastHit> closestHit = hits.Where(hit => hit.distance == closestDist);

        // Get the first hit object out of the things the player collides with
        hit = closestHit.FirstOrDefault();

        // Return if any objects were hit
        return didHit;
    }
}
