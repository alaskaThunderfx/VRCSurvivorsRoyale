using UdonSharp;
using UnityEngine;
using VRC.Udon.Common.Interfaces;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon;

public class KnifePool : UdonSharpBehaviour
{
    [Header("Owner Information")]
    public VRCPlayerApi Owner;

    // Player tracking
    public Vector3 PlayerPosition;
    public Quaternion PlayerRotation;
    public Transform StartSpot;

    [Header("Prefab Components")]
    // Main script attached to player
    public PlayerController PlayerController;

    // Script attached to the EffectsContainer
    public EffectsContainer EffectsContainer;
    public PlayerUI PlayerUI;
    public Scoreboard Scoreboard;

    [Header("Knife Stats")]
    // Knife array
    public GameObject[] Knives = new GameObject[30];

    // Used to toggle the knife weapon
    [UdonSynced]
    public bool isKnifeOn = false;

    // Index used for iterating through the pool
    public int KnifeIndex;

    [UdonSynced]
    public int Level;

    [UdonSynced, FieldChangeCallback(nameof(XP))]
    public float xP;
    public float XPToNextLv;

    // [UdonSynced, FieldChangeCallback(nameof(HP))]
    public float hP;

    // HPlvl tracker
    public int TrackHPLv;

    [UdonSynced]
    public float DEF;
    public int TrackDEFLv;

    [UdonSynced, FieldChangeCallback(nameof(RunSpeed))]
    public float runSpeed;
    public int TrackRSLv;

    // Amount of damage done on hit
    [UdonSynced]
    public float Damage;
    public int TrackDMGLv;

    // Attack frequency
    [UdonSynced]
    public float Cooldown;
    public int TrackCDLv;

    // Distance knives get thrown
    [UdonSynced]
    public float Range;
    public int TrackRangeLv;

    // Amount of knives thrown at once
    [UdonSynced]
    public float Quantity;
    public int TrackQTYLv;

    [UdonSynced]
    public float Size;
    public int TrackSizeLv;

    // Throw speed
    [UdonSynced]
    public float Force;
    public int TrackForceLv;

    // Counter for the cooldown
    public float CDCounter;

    // Upgrade tracker
    public int[] Upgrades = new int[9];

    // Correlating value for index position
    // 0 - HP
    // 1 - DEF
    // 2 - RunSpeed
    // 3 - AttackDMG
    // 4 - Cooldown
    // 5 - AttackSpeed
    // 6 - Range
    // 7 - Size
    // 8 - Quantity

    public bool ReadyToGo = false;

    // Script ran after the _OnOwnerSet script on the PlayerController
    public void _OnOwnerSet()
    {
        Owner = Networking.GetOwner(gameObject);
        PlayerController = transform.parent.GetComponent<PlayerController>();
        EffectsContainer = PlayerController.EffectsContainer;

        // Reset stats to base
        Level = 1;
        XP = 0;
        XPToNextLv = 3f;
        HP = 10f;
        TrackHPLv = 0;
        DEF = 0f;
        TrackDEFLv = 0;
        RunSpeed = 5f;
        TrackRSLv = 0;
        Damage = 1f;
        TrackDMGLv = 0;
        Cooldown = 2f;
        TrackCDLv = 0;
        Range = 100f;
        TrackRangeLv = 0;
        Quantity = 1f;
        TrackQTYLv = 0;
        Size = 1f;
        TrackSizeLv = 0;
        Force = 2f;
        TrackForceLv = 0;

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

        for (int i = 0; i < Upgrades.Length; i++)
        {
            Upgrades[i] = 0;
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
                CDCounter = Cooldown;
                for (int i = 1; i <= Quantity; i++)
                {
                    Knives[KnifeIndex].SetActive(true);
                    if (KnifeIndex <= 28)
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
    }

    public float HP
    {
        set
        {
            hP = value;
            if (ReadyToGo)
            {
                PlayerUI.SetHealth(hP);
                if (hP <= 0)
                {
                    StartSpot = GameObject.Find("StartSpot").transform;
                    Owner.TeleportTo(StartSpot.position, StartSpot.rotation);
                }
            }
        }
        get { return hP; }
    }

    public float XP
    {
        set
        {
            xP = value;
            if (Networking.LocalPlayer == Owner)
            {
                if (!ReadyToGo)
                    return;
                Scoreboard = PlayerController.Scoreboard.GetComponent<Scoreboard>();
                Scoreboard.SendCustomNetworkEvent(NetworkEventTarget.All, "UpdateBoard");
                if (xP >= XPToNextLv)
                {
                    Level++;
                    EffectsContainer.SendCustomNetworkEvent(
                        NetworkEventTarget.All,
                        nameof(LevelUp)
                    );
                    XPToNextLv *= 1.5f;
                    PlayerController.LevelUpUI.gameObject.SetActive(true);
                }
            }
        }
        get { return xP; }
    }

    public float RunSpeed
    {
        set
        {
            runSpeed = value;
            Owner.SetRunSpeed(RunSpeed);
        }
        get { return runSpeed; }
    }

    public void LevelUp() { }

    public void TestingUI()
    {
        Debug.Log("In KnifePool");
        Debug.Log("isKnifeOn");
        Debug.Log(isKnifeOn);
    }
}
