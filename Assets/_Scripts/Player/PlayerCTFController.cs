using FishNet.Connection;
using FishNet.Object;
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
            Vector3 dropPosition = transform.parent.GetComponent<PlayerMovement>().GetLastGroundedPosition();
            flagInPossession.DropFromPlayerServer(dropPosition, gameObject);
            flagInPossession.DropFromPlayer(dropPosition, gameObject);
        }
    }

    public void CheckOnCaptureZone(Transform floor)
    {
        if (flagInPossession == null) return;

        bool flagIsRed = flagInPossession.GetTeam();
        bool playerIsRed = teamColor;
        bool onRedZone = floor.CompareTag("RedCaptureZone");
        bool onBlueZone = floor.CompareTag("BlueCaptureZone");

        if (onRedZone && flagIsRed && playerIsRed)
        {
            flagInPossession.RespawnFlagServer(gameObject);
        }
        else if (onRedZone && !flagIsRed && playerIsRed)
        {
            flagInPossession.ScoreFlagServer(true, gameObject);
        }
        else if (onBlueZone && !flagIsRed && !playerIsRed)
        {
            flagInPossession.RespawnFlagServer(gameObject);
        }
        else if (onBlueZone && flagIsRed && !playerIsRed)
        {
            flagInPossession.ScoreFlagServer(false, gameObject);
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
