using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class PlayerUIContainer : UdonSharpBehaviour
{
    public bool IsReady;
    public VRCPlayerApi Owner;
    public float Scale;
    public bool SetScale;

    private void Update()
    {
        if (IsReady)
        {
            if (!SetScale)
            {
                // player hand position
                Vector3 PH = Owner.GetBonePosition(HumanBodyBones.LeftHand);
                // player elbow position
                Vector3 PE = Owner.GetBonePosition(HumanBodyBones.LeftLowerArm);
                // Scale = (PE - PH).y;
                // transform.localScale = new Vector3(Scale, Scale, Scale);
                SetScale = true;
            }
            // Vector3 PlayerPosition = Owner.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            Vector3 PlayerHand = Owner.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
            // Vector3 PlayerHand = Owner.GetBonePosition(HumanBodyBones.LeftHand);
            Quaternion PlayerHandR = Owner.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).rotation;
            // Quaternion PlayerHandR = Owner.GetBoneRotation(HumanBodyBones.LeftHand);
            // Vector3 PlayerElbow = Owner.GetBonePosition(HumanBodyBones.LeftLowerArm);


            // Debug.Log("Distance betwen hand and elbow" + (PlayerElbow - PlayerHand));

            // Owner.GetBonePosition()

            transform.position = PlayerHand;
            transform.rotation = Quaternion.Lerp(transform.rotation, PlayerHandR, .1f);
        }
    }
}
