using System.Collections;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerTackle : NetworkBehaviour
{
    [SerializeField] private GameObject flagHolder;
    [SerializeField] private float tackleForce = 5f;

    PlayerMovement playerMovement;
    public void Start()
    {
        playerMovement = transform.parent.gameObject.GetComponent<PlayerMovement>();
    }
    public void CollideWithObject(Collider other, Vector3 collisionForce)
    {
        if (!base.IsOwner) return;
        if (other.gameObject.tag == "Flag" && playerMovement.CanTackle())
        {
            Flag flag = other.gameObject.GetComponent<Flag>();

            flag.AttachToPlayer(flagHolder);
            flag.AttachToPlayerServer(flagHolder);
        }

        if (other.gameObject.tag == "Player" && playerMovement.CanTackle())
        {
            Vector3 newCollision = new Vector3(collisionForce.x * tackleForce, 0f, collisionForce.z * tackleForce);
            other.gameObject.GetComponent<PlayerTackle>().TacklePlayerServer(newCollision);
            other.transform.parent.GetComponent<PlayerMovement>().EnableRagdoll(newCollision);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TacklePlayerServer(Vector3 collisionForce, NetworkConnection conn = null)
    {
        TacklePlayer(collisionForce, conn);
    }

    [ObserversRpc]
    private void TacklePlayer(Vector3 collisionForce, NetworkConnection conn)
    {
        if (!base.LocalConnection.Equals(conn))
        {
            Debug.Log("Tackle via network!");
            transform.parent.GetComponent<PlayerMovement>().EnableRagdoll(collisionForce);
        }
    }
}
