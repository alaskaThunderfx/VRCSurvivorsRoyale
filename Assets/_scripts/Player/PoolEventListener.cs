using JetBrains.Annotations;
using UdonSharp;
using UnityEngine;
using VRC.Udon.Common.Interfaces;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon;

public class PoolEventListener : UdonSharpBehaviour
{
    // public GameObject TestingUIBG;
    // public GameObject TestingUI;
    public Scoreboard Scoreboard;
    public LilSnekSpawner LilSnekPool;
    public StartArea StartArea;

    [UdonSynced]
    public bool GameStarted = false;

    // This event is called when the local player's pool object has been assigned.
    public void _OnLocalPlayerAssigned()
    {
        // TestingUIBG.SetActive(true);
        // TestingUI.SetActive(true);
        if (Networking.IsMaster && !GameStarted)
        {
            GameStarted = true;
            // LilSnekPool.SpawnSneks();
        }
        Debug.Log("In PoolEventListener > _OnPlayerAssigned()");
        Scoreboard.SendCustomNetworkEvent(NetworkEventTarget.All, "UpdateBoard");
        StartArea.UpdatePlayersReady();
    }

    // This event is called when any player is assigned a pool object.
    // The variables will be set before the event is called.
    public VRCPlayerApi playerAssignedPlayer;
    public int playerAssignedIndex;
    public UdonBehaviour playerAssignedPoolObject;

    public void _OnPlayerAssigned()
    {
        Debug.Log("In PoolEventListener > _OnPlayerAssigned()");
        Scoreboard.SendCustomNetworkEvent(NetworkEventTarget.All, "UpdateBoard");
        StartArea.UpdatePlayersReady();
    }

    // This event is called when any player's object has been unassigned.
    // The variables will be set before the event is called.
    public VRCPlayerApi playerUnassignedPlayer;
    public int playerUnassignedIndex;
    public UdonBehaviour playerUnassignedPoolObject;

    public void _OnPlayerUnassigned()
    {
        Debug.Log("In PoolEventListener > _OnPlayerAssigned()");
        Scoreboard.SendCustomNetworkEvent(NetworkEventTarget.All, "UpdateBoard");
        StartArea.UpdatePlayersReady();
    }
}
