using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class KnifePool : UdonSharpBehaviour
{
    [Header("Owner Information")]
    public VRCPlayerApi Owner;

    // Player tracking
    public Vector3 PlayerPosition;
    public Quaternion PlayerRotation;

    [Header("Prefab Components")]
    // Main script attached to player
    public PlayerController PlayerController;

    // Script attached to the EffectsContainer
    public EffectsContainer EffectsContainer;

    [Header("Knife Stats")]
    // Knife array
    public GameObject[] Knives = new GameObject[20];

    // Used to toggle the knife weapon
    [UdonSynced]
    public bool isKnifeOn = false;

    // Index used for iterating through the pool
    public int KnifeIndex;

    // Used to track whether attacking is on or off
    public bool isAttacking;

    // Amount of damage done on hit
    public float Damage;

    // Attack frequency
    public float cooldown;

    // Counter for the cooldown
    public float CDCounter;

    // Distance knives get thrown
    public float Range;

    // Throw speed
    public float Force;

    public bool ReadyToGo = false;

    // Script ran after the _OnOwnerSet script on the PlayerController
    public void _OnOwnerSet()
    {
        Owner = Networking.GetOwner(gameObject);
        PlayerController = transform.parent.GetComponent<PlayerController>();
        EffectsContainer = PlayerController.EffectsContainer;

        // Reset stats to base
        Damage = 1f;
        cooldown = 2f;
        Range = 100f;
        Force = 2f;

        int index = 0;
        foreach (Transform child in transform)
        {
            Networking.SetOwner(Owner, child.gameObject);
            Knife Knife = child.GetComponent<Knife>();
            Knife.name = index.ToString() + "Knife";
            Knives[index] = Knife.gameObject;
            Knife._OnOwnerSet();
            index++;
        }
        ReadyToGo = true;
    }

    private void Update()
    {
        if (Owner == null || !ReadyToGo)
        {
            return;
        }
        // Tracking the players position
        PlayerPosition = Owner.GetPosition();
        PlayerRotation = Owner.GetRotation();

        // Used to toggle knife attacking
        // If isAttacking is false, stop attacking
        if (!isKnifeOn)
        {
            return;
        }
        // if isAttacking is true, begin the attack
        else
        {
            CDCounter -= Time.deltaTime;
            if (CDCounter <= 0)
            {
                CDCounter = cooldown;
                Knives[KnifeIndex].SetActive(true);
                if (KnifeIndex <= 18)
                {
                    KnifeIndex++;
                }
                else
                {
                    KnifeIndex = 0;
                }
            }
        }
    }

    public void TestingUI()
    {
        Debug.Log("In KnifePool");
        Debug.Log("isKnifeOn");
        Debug.Log(isKnifeOn);
    }
}
