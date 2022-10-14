using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class TestingUI : UdonSharpBehaviour
{
    public Toggle KnifeToggle;
    public Cyan.PlayerObjectPool.CyanPlayerObjectAssigner PlayerPool;
    public PlayerController PlayerController;

    private void OnEnable()
    {
        PlayerController = PlayerPool
            ._GetPlayerPooledObject(Networking.LocalPlayer)
            .GetComponent<PlayerController>();
    }

    public void ToggleKnife()
    {
        Debug.Log("Pressing TogleKnife");
        PlayerController.TestingUi();
        PlayerController.KnifePool.isKnifeOn = !PlayerController.KnifePool.isKnifeOn;
    }
}
