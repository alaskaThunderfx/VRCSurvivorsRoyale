using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class KnifeToggleTestingUI : UdonSharpBehaviour
{
    public Slider QuantitySlider;
    public Text QuantityText;
    public Slider DamageSlider;
    public Text DamageText;
    public Slider CooldownSlider;
    public Text CooldownText;
    public Slider RangeSlider;
    public Text RangeText;
    public Slider SpeedSlider;
    public Text SpeedText;
    public Slider SizeSlider;
    public Text SizeText;
    public TestingUI TestingUI;
    public KnifePool KnifePool;

    private void OnEnable() {
        KnifePool = TestingUI.PlayerController.KnifePool;
    }

    public void UpdateTab()
    {
        QuantityText.text = QuantitySlider.value.ToString();
        KnifePool.Quantity = QuantitySlider.value;
        DamageText.text = DamageSlider.value.ToString();
        KnifePool.Damage = DamageSlider.value;
        CooldownText.text = CooldownSlider.value.ToString();
        KnifePool.cooldown = CooldownSlider.value;
        RangeText.text = RangeSlider.value.ToString();
        KnifePool.Range = RangeSlider.value;
        SpeedText.text = SpeedSlider.value.ToString();
        KnifePool.Force = SpeedSlider.value;
        SizeText.text = SizeSlider.value.ToString();
        KnifePool.Size = SizeSlider.value;
    }
}
