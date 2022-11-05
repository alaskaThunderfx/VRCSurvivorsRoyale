﻿
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class FireballSelect : UdonSharpBehaviour
{
    public Cyan.PlayerObjectPool.CyanPlayerObjectAssigner PlayerObjectAssigner;
    public PlayerController PlayerController;

    public override void Interact()
    {
        VRCPlayerApi Owner = Networking.GetOwner(gameObject);
        if (Networking.LocalPlayer != Owner)
        {
            Owner = Networking.LocalPlayer;
            Networking.SetOwner(Owner, gameObject);
        }

        PlayerController = PlayerObjectAssigner
            ._GetPlayerPooledObject(Owner)
            .GetComponent<PlayerController>();

        PlayerController.Weapon = "Fireball";
    }
}
