using UdonSharp;
using UnityEngine;
using VRC.Udon.Common.Interfaces;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon;

public class FireballPool : UdonSharpBehaviour
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

    [Header("Fireball Stats")]
    // Fireball array
    public GameObject[] Knives = new GameObject[30];

    // Used to toggle the Fireball weapon
    [UdonSynced]
    public bool isFireballOn = false;

    // Index used for iterating through the pool
    public int FireballIndex;

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
    public bool SwitchOnUI = false;

    private void OnEnable() {
        _OnOwnerSet();
        isFireballOn = true;
    }

    // Script ran after the _OnOwnerSet script on the PlayerController
    public void _OnOwnerSet()
    {
        PlayerController = transform.parent.GetComponent<PlayerController>();
        Owner = PlayerController.Owner;
        PlayerUI = GameObject.Find("PlayerUI").GetComponent<PlayerUI>();
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
            Fireball Fireball = child.GetComponent<Fireball>();
            Fireball.name = index.ToString() + "Fireball";
            Knives[index] = Fireball.gameObject;
            Fireball._OnOwnerSet();
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

        if (SwitchOnUI)
        {
            PlayerController.PlayerUI.AttackToggleOn.gameObject.SetActive(true);
            SwitchOnUI = false;
        }

        // Used to toggle Fireball attacking
        // If isFireballOn is false, stop attacking
        if (!isFireballOn)
        {
            return;
        }
        // if isFireballOn is true, begin the attack
        else
        {
            // Countdown from the CDCounter
            CDCounter -= Time.deltaTime;
            // Once the CDCounter is less than or equal to 0
            if (CDCounter <= 0)
            {
                // Reset CDCounter to Cooldown value
                CDCounter = Cooldown;
                // Begin for loop that corresponds with Quantity value
                // Trying a switch statement instead
                switch (Quantity)
                {
                    case 1:
                        ThrowFireball();
                        break;
                    case 2:
                        ThrowFireball();
                        SendCustomEventDelayedSeconds(nameof(ThrowFireball), 0.1f);
                        break;
                    case 3:
                        ThrowFireball();
                        SendCustomEventDelayedSeconds(nameof(ThrowFireball), 0.1f);
                        SendCustomEventDelayedSeconds(nameof(ThrowFireball), 0.2f);
                        break;
                    case 4:
                        ThrowFireball();
                        SendCustomEventDelayedSeconds(nameof(ThrowFireball), 0.1f);
                        SendCustomEventDelayedSeconds(nameof(ThrowFireball), 0.2f);
                        SendCustomEventDelayedSeconds(nameof(ThrowFireball), 0.3f);
                        break;
                    case 5:
                        ThrowFireball();
                        SendCustomEventDelayedSeconds(nameof(ThrowFireball), 0.1f);
                        SendCustomEventDelayedSeconds(nameof(ThrowFireball), 0.2f);
                        SendCustomEventDelayedSeconds(nameof(ThrowFireball), 0.3f);
                        SendCustomEventDelayedSeconds(nameof(ThrowFireball), 0.4f);
                        break;
                }
            }
        }
    }

    public void ThrowFireball()
    {
        // Set Fireball at this index in the object pool to active
        Knives[FireballIndex].SetActive(true);
        // If the value of Fireball index is less than or equal to 28
        if (FireballIndex <= 28)
        {
            // Increase FireballIndex value
            FireballIndex++;
        }
        // If the value is 29
        else
        {
            // Reset value to zaro
            FireballIndex = 0;
        }
    }

    public float maxHP;
    public float MaxHP
    {
        set
        {
            maxHP = HP + value;
            Debug.Log("In MaxHP, value of MaxHP is " + maxHP);
            PlayerController.PlayerUI.SetMaxHealth(maxHP);
            if (HP + (maxHP - HP) > maxHP)
            {
                PlayerController.PlayerUI.SetHealth(maxHP);
            }
            else
            {
                HP += 10;
            }
        }
        get { return maxHP; }
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
                if (xP >= XPToNextLv && Level <= 20)
                {
                    Level++;
                    EffectsContainer.SendCustomNetworkEvent(
                        NetworkEventTarget.All,
                        nameof(LevelUp)
                    );
                    XPToNextLv *= 1.2f;
                    PlayerController.LevelUpUI.gameObject.SetActive(true);
                    PlayerController.SetUIWAS("Fireball", Level.ToString());
                    xP = 0;
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
            if (Networking.LocalPlayer == Owner)
            {
                Owner.SetRunSpeed(RunSpeed);
            }
        }
        get { return runSpeed; }
    }

    public void LevelUp() { }

    public void TestingUI()
    {
        Debug.Log("In FireballPool");
        Debug.Log("isFireballOn");
        Debug.Log(isFireballOn);
    }
}
