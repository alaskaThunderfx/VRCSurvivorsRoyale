using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

public class DetectionSphere : UdonSharpBehaviour
{
    public LilSnek LilSnek;
    public Cyan.PlayerObjectPool.CyanPlayerObjectAssigner PlayersPool;
    public Component[] Players;
    public VRCPlayerApi Owner;

    private void OnEnable()
    {
        LilSnek = transform.parent.GetComponent<LilSnek>();
        PlayersPool = GameObject
            .Find("PlayerObjectAssigner")
            .GetComponent<Cyan.PlayerObjectPool.CyanPlayerObjectAssigner>();
        Players = PlayersPool._GetActivePoolObjects();
        Owner = Networking.GetOwner(gameObject);
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player != Networking.GetOwner(gameObject))
        {
            Networking.SetOwner(player, gameObject);
        }
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ChasingPlayer));
    }

    public void ChasingPlayer()
    {
        LilSnek.Owner = Owner;
        LilSnek.CurrentState = 1;
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        Owner = player;
    }
}
