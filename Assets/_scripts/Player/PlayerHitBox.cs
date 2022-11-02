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

    // When the enemy attack trigger enter the PlayerHitBox
    private void OnTriggerEnter(Collider other)
    {
        if (other.name.Contains("Hit"))
        {
            Transform Attacker = other.transform.parent.parent;
            if (Attacker.name.Contains("LilSnek"))
            {
                // Get the script from the scpeific LilSnek
                LilSnek ThisSnake = Attacker.GetComponent<LilSnek>();
                // Reduce the HP of the player based on the damage of the LilSnek and the amount of Defense the player has
                PlayerController.KnifePool.HP -= (ThisSnake.DMG - PlayerController.KnifePool.DEF);
                // For debugging purposes
                Debug.Log(
                    Owner
                        + " got bit by "
                        + ThisSnake.name
                        + "!\nHP Remaining: "
                        + PlayerController.KnifePool.HP
                );
            }
            else if (Attacker.name.Contains("LilWolf"))
            {
                Debug.Log(other.transform.parent.parent.name);
                // Get teh root Transform of the specific LilWolf
                LilWolf ThisWolf = Attacker.GetComponent<LilWolf>();
                Debug.Log(ThisWolf.name);
                // Reduce the HP of the player based on the damage of the LilWolf and the amount of Defense the player has
                PlayerController.KnifePool.HP -= (ThisWolf.DMG - PlayerController.KnifePool.DEF);
                // For debugging purposes
                Debug.Log(
                    Owner
                        + " got bit by "
                        + ThisWolf.name
                        + "!\nHP Remaining: "
                        + PlayerController.KnifePool.HP
                );
            }
            else if (Attacker.name.Contains("LilGhost"))
            {
                Debug.Log(other.transform.parent.parent.name);
                // Get teh root Transform of the specific LilWolf
                LilGhost ThisGhost = Attacker.GetComponent<LilGhost>();
                Debug.Log(ThisGhost.name);
                // Reduce the HP of the player based on the damage of the LilGhost and the amount of Defense the player has
                PlayerController.KnifePool.HP -= (ThisGhost.DMG - PlayerController.KnifePool.DEF);
                // For debugging purposes
                Debug.Log(
                    Owner
                        + " got bit by "
                        + ThisGhost.name
                        + "!\nHP Remaining: "
                        + PlayerController.KnifePool.HP
                );
            }
        }
    }
}
