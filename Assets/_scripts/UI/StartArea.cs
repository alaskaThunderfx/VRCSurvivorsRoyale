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

    [UdonSynced]
    public int numberOfPlayersReady;
    public Button StartButton;
    public Transform TargetTransform;
    public VRCPlayerApi[] Players;

    public void UpdatePlayersReady()
    {
        PlayersReady.text = "Players Ready:\n" + NumberOfPlayersReady.ToString();
    }

    public void StartGame()
    {
        Debug.Log("Pressed StartGame~!");
        // VRCPlayerApi.GetPlayers(Players);
        // foreach (VRCPlayerApi player in Players)
        // {
        //     player.TeleportTo(TargetTransform.position, TargetTransform.rotation);
        // }
    }

    public int NumberOfPlayersReady
    {
        set
        {
            numberOfPlayersReady = value;
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(UpdatePlayersReady));
        }
        get { return numberOfPlayersReady; }
    }
}
