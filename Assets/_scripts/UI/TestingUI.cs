using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class TestingUI : UdonSharpBehaviour
{
    public Toggle KnifeToggle;
    public GameObject KnifeUI;
    public GameObject MusicControl;
    public Cyan.PlayerObjectPool.CyanPlayerObjectAssigner PlayerPool;
    public PlayerController PlayerController;

    private void OnEnable()
    {
        PlayerController = PlayerPool
            ._GetPlayerPooledObject(Networking.LocalPlayer)
            .GetComponent<PlayerController>();
        MusicControl.SetActive(true);
    }

    public void ToggleKnife()
    {
        Debug.Log("Pressing TogleKnife");
        PlayerController.TestingUi();
        PlayerController.KnifePool.isKnifeOn = !PlayerController.KnifePool.isKnifeOn;
        if (PlayerController.KnifePool.isKnifeOn)
        {
            KnifeUI.SetActive(true);
        }
        else if (!PlayerController.KnifePool.isKnifeOn)
        {
            KnifeUI.SetActive(false);
        }
    }
}
