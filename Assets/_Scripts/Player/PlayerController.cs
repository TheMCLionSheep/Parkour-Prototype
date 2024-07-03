using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;

public class PlayerController : NetworkBehaviour
{
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (base.IsOwner)
        {
            GetComponent<PlayerMovement>().EnableCamera();
        }
        else
        {
            GetComponent<PlayerMovement>().enabled = false;
        }
    }
}
