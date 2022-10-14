
using JetBrains.Annotations;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class PoolEventListener : UdonSharpBehaviour
{
    public GameObject TestingUIBG;
    public GameObject TestingUI;
    // This event is called when the local player's pool object has been assigned.
    public void _OnLocalPlayerAssigned()
    {
        TestingUIBG.SetActive(true);
        TestingUI.SetActive(true);
    }
    
    // This event is called when any player is assigned a pool object.
    // The variables will be set before the event is called.
    public VRCPlayerApi playerAssignedPlayer;
    public int playerAssignedIndex;
    public UdonBehaviour playerAssignedPoolObject;
    
    public void _OnPlayerAssigned()
    {
        
    }
    
    // This event is called when any player's object has been unassigned.
    // The variables will be set before the event is called.
    public VRCPlayerApi playerUnassignedPlayer;
    public int playerUnassignedIndex;
    public UdonBehaviour playerUnassignedPoolObject;
    
    public void _OnPlayerUnassigned()
    {
        
    }
}
