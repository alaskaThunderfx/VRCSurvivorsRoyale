using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class PlayerHitBox : UdonSharpBehaviour
{
    public VRCPlayerApi Owner;
    public PlayerController PlayerController;
    public bool IsReady;

    public void _OnOwnerSet()
    {
        Owner = Networking.GetOwner(gameObject);
        PlayerController = transform.parent.GetComponent<PlayerController>();
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

    private void OnTriggerEnter(Collider other) {
        Transform AttackedBy = other.transform.parent.parent;
        if (AttackedBy.name.Contains("LilSnek"))
        {
            LilSnek ThisSnake = AttackedBy.GetComponent<LilSnek>();
            PlayerController.KnifePool.HP -= (ThisSnake.DMG - PlayerController.KnifePool.DEF);
            Debug.Log(Owner + " got bit by " + ThisSnake.name + "!\nHP Remaining: " + PlayerController.KnifePool.HP);
        }
    }
}
