using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon.Common.Interfaces;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon;

public class StartArea : UdonSharpBehaviour
{
    public Text PlayersReady;
    public LilSnekSpawner LilSnekPool;
    public LilWolfSpawner LilWolfPool;
    public LilGhostSpawner LilGhostPool;
    public Cyan.PlayerObjectPool.CyanPlayerObjectAssigner PlayerPool;

    [UdonSynced]
    public int NumberOfPlayersReady;
    public Component[] Players;
    public Button StartButton;
    public Transform TargetTransform;

    public void UpdatePlayersReady()
    {
        Players = PlayerPool._GetActivePoolObjects();
        Debug.Log("Number of active players == " + Players.Length);
        NumberOfPlayersReady = Players.Length;
        PlayersReady.text = "Players Ready:\n" + NumberOfPlayersReady.ToString();
        // RequestSerialization();
    }

    public void StartGame()
    {
        Debug.Log("Pressed StartGame~!");
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(TeleportPlayers));
        LilSnekPool.SendCustomNetworkEvent(NetworkEventTarget.Owner, "SpawnSneks");
        LilWolfPool.SendCustomNetworkEvent(NetworkEventTarget.Owner, "SpawnWolfs");
        LilGhostPool.SendCustomNetworkEvent(NetworkEventTarget.Owner, "SpawnGhosts");
    }

    public void TeleportPlayers()
    {
        Networking.LocalPlayer.TeleportTo(TargetTransform.position, TargetTransform.rotation);
        PlayerController PlayerController = PlayerPool
            ._GetPlayerPooledObject(Networking.LocalPlayer)
            .GetComponent<PlayerController>();
        string Weapon = PlayerController.Weapon;
        switch (Weapon)
        {
            case "Knife":
                PlayerController.LevelUpUI.gameObject.SetActive(true);
                PlayerController.KnifePool.isKnifeOn = true;
                PlayerController.KnifePool.SwitchOnUI = true;
                break;
            case "Fireball":
                PlayerController.FireballPool.isFireballOn = true;
                PlayerController.FireballPool.SwitchOnUI = true;
                break;
        }
    }

    // public override void OnDeserialization()
    // {
    //     if (!Networking.IsMaster)
    //     {
    //         PlayersReady.text = "Players Ready:\n" + NumberOfPlayersReady.ToString();
    //     }
    // }

    // public int NumberOfPlayersReady
    // {
    //     set
    //     {
    //         numberOfPlayersReady = value;
    //         SendCustomNetworkEvent(NetworkEventTarget.All, nameof(UpdatePlayersReady));
    //     }
    //     get { return numberOfPlayersReady; }
    // }
}
