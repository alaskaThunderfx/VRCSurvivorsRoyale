using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class PlayerHitBox : UdonSharpBehaviour
{
    public VRCPlayerApi Owner;
    public bool IsReady;

    public void _OnOwnerSet()
    {
        Owner = Networking.GetOwner(gameObject);
        IsReady = true;
        Debug.Log("Set the hitbox owner");
    }

    private void Update()
    {
        if (IsReady)
        {
            transform.position = Owner.GetPosition();
        }
    }
}
