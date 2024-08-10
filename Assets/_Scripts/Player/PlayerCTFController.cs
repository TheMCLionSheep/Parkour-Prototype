using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerCTFController : NetworkBehaviour
{
    [SerializeField] private GameObject flagHolder;
    [SerializeField] private float tackleForce = 5f;

    [SerializeField] private float voidLevel;

    private readonly SyncVar<bool> teamColor = new SyncVar<bool>(new SyncTypeSettings(WritePermission.ClientUnsynchronized, ReadPermission.ExcludeOwner));
    private Flag flagInPossession;
    private Transform spawnPoint;
    PlayerMovement playerMovement;

    public void Start()
    {
        playerMovement = transform.parent.gameObject.GetComponent<PlayerMovement>();
        UpdateSpawn();
    }

    private void Update()
    {
        if (!base.IsOwner) return;

        if (Input.GetKeyDown(KeyCode.T))
        {
            Debug.Log("Current team: " + teamColor.Value + ", " + gameObject.GetInstanceID());
            ChangeTeamsServer(!teamColor.Value);
            ChangeTeam();
        }

        // If the player is below void level, respawn player
        if (transform.position.y <= voidLevel)
        {            
            DropPlayerFlag();
            RespawnPlayer();
        }
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

    public void UpdateSpawn()
    {
        spawnPoint = teamColor.Value ? GameObject.FindWithTag("RedSpawn").transform : GameObject.FindWithTag("BlueSpawn").transform;
    }

    public void RespawnPlayer()
    {
        Debug.Log("Respawn");
        playerMovement.ResetPlayer();
        transform.parent.position = spawnPoint.position;
    }

    [ServerRpc(RunLocally = true)]
    public void ChangeTeamsServer(bool newTeam)
    {
        teamColor.Value = newTeam;
        Debug.Log("Change to: " + teamColor.Value + ", " + gameObject.GetInstanceID());
    }

    public void ChangeTeam()
    {
        Debug.Log("Team: " + teamColor.Value + ", " + gameObject.GetInstanceID());
        UpdateSpawn();
        DropPlayerFlag();
        RespawnPlayer();
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
        bool playerIsRed = teamColor.Value;
        bool onRedZone = floor.CompareTag("RedCaptureZone");
        bool onBlueZone = floor.CompareTag("BlueCaptureZone");

        Debug.Log("On capture: " + playerIsRed);

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
