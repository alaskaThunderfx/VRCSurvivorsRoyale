using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class KnifeToggleTestingUI : UdonSharpBehaviour
{
    public Slider QuantitySlider;
    public Text QuantityText;
    public TestingUI TestingUI;
    public KnifePool KnifePool;

    private void OnEnable() {
        KnifePool = TestingUI.PlayerController.KnifePool;
    }

    public void UpdateTab()
    {
        QuantityText.text = QuantitySlider.value.ToString();
        KnifePool.Quantity = QuantitySlider.value;
    }
}
