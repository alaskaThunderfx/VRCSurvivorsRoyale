using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class LevelUpUIContainer : UdonSharpBehaviour
{
    public bool IsReady;
    public VRCPlayerApi Owner;
    public LevelUp LevelUp;

    private void Update() {
        if (IsReady)
        {
            Vector3 PlayerPosition = Owner.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;

            transform.position = new Vector3(PlayerPosition.x, PlayerPosition.y, PlayerPosition.z);
            transform.rotation = Quaternion.Lerp(transform.rotation, Owner.GetRotation(), .1f);
        }
    }
}
