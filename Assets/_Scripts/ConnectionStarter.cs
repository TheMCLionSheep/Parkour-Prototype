using System;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using UnityEngine;

public class ConnectionStarter : MonoBehaviour
{
    // private Tugboat tugboat;

    // private void OnEnable()
    // {
    //     InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionState;
    // }

    // private void OnDisable()
    // {
    //     InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionState;
    // }

    // private void OnClientConnectionState(ClientConnectionStateArgs args)
    // {
    //     if (args.ConnectionState == LocalConnectionState.Stopping)
    //     {
    //         UnityEditor.EditorApplication.isPlaying = false;
    //     }
    // }

    // void Start()
    // {
    //     if(TryGetComponent(out Tugboat t))
    //     {
    //         tugboat = t;
    //     }
    //     else
    //     {
    //         Debug.LogError("Couldn't get tugboat!", this);
    //         return;
    //     }

    //     if (ParrelSync.ClonesManager.IsClone())
    //     {
    //         tugboat.StartConnection(false);
    //     }   
    //     else
    //     {
    //         tugboat.StartConnection(true);
    //         tugboat.StartConnection(false);
    //     }
    // }
}
