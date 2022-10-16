using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class KnifeToggleTestingUI : UdonSharpBehaviour
{
    public Slider slider;
    public Text text;
    public TestingUI TestingUI;
    public PlayerController PlayerController;

    public void UpdateTab()
    {
        text.text = slider.value.ToString();
    }
}
