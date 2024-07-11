using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSpawn : MonoBehaviour
{
    [SerializeField] private float voidLevel;

    private Transform spawnPoint;

    public void Start()
    {
        spawnPoint = GameObject.FindWithTag("RedSpawn").transform;
    }
    public void RespawnPlayer()
    {
        transform.position = spawnPoint.position;
    }

    public void Update()
    {
        if (transform.position.y <= voidLevel)
        {
            RespawnPlayer();
        }
    }
}
