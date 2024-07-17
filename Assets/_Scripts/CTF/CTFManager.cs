using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;

public class CTFManager : NetworkBehaviour
{
    public static CTFManager Instance { get; private set; }

    [SerializeField] TMP_Text redPointTextDisplay;
    [SerializeField] TMP_Text bluePointTextDisplay;

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
