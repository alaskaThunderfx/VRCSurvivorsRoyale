using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

public class LilWolfDetectionSphere : UdonSharpBehaviour
{
    public LilWolf LilWolf;
    public Cyan.PlayerObjectPool.CyanPlayerObjectAssigner PlayersPool;
    public Component[] Players;
    public VRCPlayerApi Owner;

    private void OnEnable()
    {
        LilWolf = transform.parent.GetComponent<LilWolf>();
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
        LilWolf.Owner = Owner;
        LilWolf.CurrentState = 1;
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        Owner = player;
    }
}
