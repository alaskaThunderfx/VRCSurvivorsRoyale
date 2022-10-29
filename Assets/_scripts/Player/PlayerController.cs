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
    public KnifePool KnifePool;
    public PlayerHitBox PlayerHitBox;
    public PlayerUI PlayerUI;
    public LevelUp LevelUpUI;

    [Header("Stats")]
    public int level;

    [UdonSynced, FieldChangeCallback(nameof(Experience))]
    public float experience;
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
        EffectsContainer = transform.GetChild(0).GetComponent<EffectsContainer>();
        EffectsContainer._OnOwnerSet();
        Debug.Log("Setting owner of KnifePool");
        KnifePool = transform.GetChild(1).GetComponent<KnifePool>();
        KnifePool._OnOwnerSet();
        PlayerHitBox = transform.GetChild(2).GetComponent<PlayerHitBox>();
        PlayerHitBox._OnOwnerSet();
        Debug.Log("Before getting the PlayerUI component");
        PlayerUI = transform.GetChild(4).GetChild(0).GetComponent<PlayerUI>();
        PlayerUI._OnOwnerSet();
        Scoreboard = GameObject.Find("Scoreboard").GetComponent<Scoreboard>();
        LevelUpUI = transform.GetChild(3).GetChild(0).GetComponent<LevelUp>();
        LevelUpUI._OnOwnerSet();
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

    

    public float Experience
    {
        set
        {
            experience = value;
            if (Networking.LocalPlayer == Owner)
            {
                Scoreboard.SendCustomNetworkEvent(NetworkEventTarget.All, "UpdateBoard");
            }
        }
        get { return experience; }
    }
}
