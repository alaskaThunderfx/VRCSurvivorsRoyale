using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class PlayerUI : UdonSharpBehaviour
{
    public VRCPlayerApi Owner;
    public PlayerController PlayerController;
    public KnifePool KnifePool;
    public Text WeaponAndLevel;
    public Slider HealthBar;
    public Vector3 HeadPos;
    public Quaternion HeadRot;
    public bool IsReady;

    public void _OnOwnerSet()
    {
        Owner = Networking.GetOwner(gameObject);
        PlayerController = transform.parent.GetComponent<PlayerController>();
        KnifePool = PlayerController.KnifePool;
        KnifePool.PlayerUI = GetComponent<PlayerUI>();
        KnifePool.HP = 10f;
        SetMaxHealth(KnifePool.HP);
        IsReady = true;
    }

    private void Update()
    {
        if (!IsReady) return;
        HeadPos = Owner.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
        HeadRot = Owner.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;
        transform.SetPositionAndRotation(HeadPos, HeadRot);
        Debug.Log(Owner.playerId);
    }

    public void SetMaxHealth(float health)
    {
        HealthBar.maxValue = KnifePool.HP;
        HealthBar.value = KnifePool.HP;
    }

    public void SetHealth(float health)
    {
        HealthBar.value = KnifePool.HP;
    }
}
