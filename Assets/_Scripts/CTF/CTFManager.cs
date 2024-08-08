using System;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;

public class CTFManager : NetworkBehaviour
{
    public static CTFManager Instance { get; private set; }

    [SerializeField] TMP_Text redPointTextDisplay;
    [SerializeField] TMP_Text bluePointTextDisplay;

    [SerializeField] GameObject redFlagPrefab;
    [SerializeField] GameObject blueFlagPrefab;

    [SerializeField] Transform redFlagSpawn;
    [SerializeField] Transform blueFlagSpawn;

    private readonly SyncVar<int> redPoints = new SyncVar<int>();
    private readonly SyncVar<int> bluePoints = new SyncVar<int>();

    public void Awake()
    {
        if (Instance != null && Instance != this) 
        { 
            Destroy(this); 
        } 
        else 
        { 
            Instance = this; 
        }

        redPoints.OnChange += OnRedPointChange;
        bluePoints.OnChange += OnBluePointChange;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("SPAWNNNN");
        GameObject redFlag = Instantiate(redFlagPrefab, redFlagSpawn);
        InstanceFinder.ServerManager.Spawn(redFlag, null);

        GameObject blueFlag = Instantiate(blueFlagPrefab, blueFlagSpawn);
        InstanceFinder.ServerManager.Spawn(blueFlag, null);
    }

    private void OnRedPointChange(int prev, int next, bool asServer)
    {
        redPointTextDisplay.text = next.ToString();
    }

    private void OnBluePointChange(int prev, int next, bool asServer)
    {
        bluePointTextDisplay.text = next.ToString();
    }

    public void SetPoints(int points, bool team)
    {
        if (team)
        {
            redPoints.Value = points;
        }
        else
        {
            bluePoints.Value = points;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void AddPoint(bool team)
    {
        if (team)
        {
            redPoints.Value++;
        }
        else
        {
            bluePoints.Value++;
        }
    }
}
