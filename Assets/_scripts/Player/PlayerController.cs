using UdonSharp;
using UnityEngine;
using VRC.Udon.Common.Interfaces;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon;

public class PlayerController : UdonSharpBehaviour
{
    [Header("Components")]
    // The player associated with this script
    public VRCPlayerApi Owner;
    public EffectsContainer EffectsContainer;
    public Scoreboard Scoreboard;
    [UdonSynced, FieldChangeCallback(nameof(Weapon))]
    public string weapon;
    public KnifePool KnifePool;
    public FireballPool FireballPool;
    public PlayerHitBox PlayerHitBox;
    public PlayerUI PlayerUI;
    public LevelUp LevelUpUI;

    [Header("Stats")]
    public int level;
    public float xpToNextLevel;
    public float hp;

    // When the PlayerObjectAssigner has set the owner
    public void _OnOwnerSet()
    {
        // Rename the object to the player id of the current owner.
        gameObject.name = Owner.playerId.ToString() + "Player";
        // Iterate through child objects and set the owner to this player
        foreach (Transform child in transform)
        {
            Networking.SetOwner(Owner, child.gameObject);
        }
        EffectsContainer = transform.GetChild(1).GetComponent<EffectsContainer>();
        EffectsContainer._OnOwnerSet();
        KnifePool = transform.GetChild(2).GetComponent<KnifePool>();
        KnifePool._OnOwnerSet();
        FireballPool = transform.GetChild(3).GetComponent<FireballPool>();
        FireballPool._OnOwnerSet();
        PlayerHitBox = transform.GetChild(0).GetComponent<PlayerHitBox>();
        PlayerHitBox._OnOwnerSet();
        Scoreboard = GameObject.Find("Scoreboard").GetComponent<Scoreboard>();
        if (Networking.LocalPlayer == Owner)
        {
            PlayerUI = GameObject.Find("PlayerUI").GetComponent<PlayerUI>();
            PlayerUI.Owner = Owner;
            PlayerUI.PlayerController = GetComponent<PlayerController>();
            PlayerUI._OnOwnerSet();

            LevelUpUI = GameObject.Find("LevelUpUI").GetComponent<LevelUp>();
            LevelUpUI.Owner = Owner;
            LevelUpUI.PlayerController = GetComponent<PlayerController>();
            LevelUpUI._OnOwnerSet();
        }
    }

    private void Update() { }

    public void _OnCleanup() { }

    public void TestingUi()
    {
        Debug.Log("In PlayerController");
        KnifePool.TestingUI();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.name == "Hit")
        {
            Debug.Log("I've been hit!");
        }
    }

    public void SetUIWAS(string weapon, string level)
    {
        PlayerUI.WeaponAndStats.text = weapon + " Lv: " + level;
    }

    public string Weapon
    {
        set
        {
            weapon = value;
            switch (weapon)
            {
                case "Knife":
                    SetUIWAS("Knife", "1");
                    break;
                case "Fireball":
                    SetUIWAS("Fireball", "1");
                    break;
            }
        }
        get
        {
            return weapon;
        }
    }
}
