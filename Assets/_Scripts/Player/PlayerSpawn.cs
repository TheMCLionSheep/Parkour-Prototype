using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSpawn : MonoBehaviour
{
    private Transform spawnPoint;

    public void Start()
    {
        spawnPoint = GameObject.FindWithTag("RedSpawn").transform;
    }
    
    public void RespawnPlayer()
    {
        transform.position = spawnPoint.position;
    }
}
