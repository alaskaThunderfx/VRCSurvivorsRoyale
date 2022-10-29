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
            Vector3 PlayerHead = Owner.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            // Vector3 PlayerHead = Owner.GetBonePosition(HumanBodyBones.Head);
            Quaternion PlayerHeadR = Owner.GetRotation();
            // Quaternion PlayerHeadR = Owner.GetBoneRotation(HumanBodyBones.Head);
            // Vector3 PlayerElbow = Owner.GetBonePosition(HumanBodyBones.LeftLowerArm);


            // Debug.Log("Distance betwen Head and elbow" + (PlayerElbow - PlayerHead));

            // Owner.GetBonePosition()

            transform.position = PlayerHead;
            transform.rotation = Quaternion.Lerp(transform.rotation, PlayerHeadR, .05f);
        }
    }

    private void LateUpdate() {
         Vector3 PlayerHead = Owner.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
         transform.LookAt(PlayerHead);
    }
}
