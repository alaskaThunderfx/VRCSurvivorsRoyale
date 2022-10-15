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
        Scoreboard = GameObject.Find("Scoreboard").GetComponent<Scoreboard>();
    }

    public void _OnCleanup() { }

    public void TestingUi()
    {
        Debug.Log("In PlayerController");
        KnifePool.TestingUI();
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
