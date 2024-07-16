using System.Collections;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using Unity.VisualScripting;
using UnityEditor.PackageManager;
using UnityEngine;

public class PlayerCTFController : NetworkBehaviour
{
    private bool teamColor;

    [SerializeField] private GameObject flagHolder;
    [SerializeField] private float tackleForce = 5f;

    private Flag flagInPossession;
    PlayerMovement playerMovement;

    public void Start()
    {
        playerMovement = transform.parent.gameObject.GetComponent<PlayerMovement>();
        teamColor = true;
    }

    public void CollideWithObject(Collider other, Vector3 collisionForce)
    {
        if (!base.IsOwner) return;
        if (other.gameObject.tag == "Flag" && playerMovement.CanTackle())
        {
            DropPlayerFlag();
            
            Flag flag = other.gameObject.GetComponent<Flag>();

            flag.AttachToPlayer(gameObject);
            flag.AttachToPlayerServer(gameObject);
        }

        if (other.gameObject.tag == "Player" && playerMovement.CanTackle())
        {
            Vector3 newCollision = new Vector3(collisionForce.x * tackleForce, 0f, collisionForce.z * tackleForce);
            other.gameObject.GetComponent<PlayerCTFController>().TacklePlayerServer(newCollision);
            other.transform.parent.GetComponent<PlayerMovement>().EnableRagdoll(newCollision);
        }
    }

    public GameObject GetFlagHolder()
    {
        return flagHolder;
    }

    public void OwnFlag(Flag newFlag)
    {
        flagInPossession = newFlag;
    }

    public void DropPlayerFlag()
    {
        if (flagInPossession != null)
        {
            flagInPossession.DropFromPlayerServer(transform.parent.GetComponent<PlayerMovement>().GetLastGroundedPosition());
        }
    }

    public void CheckOnCaptureZone(Transform floor)
    {
        if (flagInPossession != null)
        {
            if (floor.CompareTag("RedCaptureZone"))
            {
                Debug.Log("On Red capture zone");
                
                // If they have a red flag and are red team, return flag
                if (flagInPossession.GetTeam() && teamColor)
                {
                    Debug.Log("Return flag");
                }
                // If they have a blue flag and are red team, get red point
                else if (!flagInPossession.GetTeam() && teamColor)
                {
                    Debug.Log("Score red point!");
                }
            }
            else if (floor.CompareTag("BlueCaptureZone"))
            {
                Debug.Log("On blue capture zone");

                // If they have a blue flag and are blue team, return flag
                if (!flagInPossession.GetTeam() && !teamColor)
                {
                    Debug.Log("Return flag");
                }
                // If they have a red flag and are blue team, get blue point
                else if (flagInPossession.GetTeam() && teamColor)
                {
                    Debug.Log("Score blue point!");
                }
            }
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

            DropPlayerFlag();
        }
    }
}
