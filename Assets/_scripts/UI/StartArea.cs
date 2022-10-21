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
        RequestSerialization();
    }

    public void StartGame()
    {
        Debug.Log("Pressed StartGame~!");
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(TeleportPlayers));
        LilSnekPool.SpawnSneks();
    }

    public void TeleportPlayers()
    {
        Networking.LocalPlayer.TeleportTo(TargetTransform.position, TargetTransform.rotation);
    }

    public override void OnDeserialization()
    {
        if (!Networking.IsMaster)
        {
            PlayersReady.text = "Players Ready:\n" + NumberOfPlayersReady.ToString();
        }
    }

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
