using UdonSharp;
using UnityEngine;
using UnityEngine.AI;
using VRC.Udon.Common.Interfaces;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon;

public class LilSnekSpawner : UdonSharpBehaviour
{
    public VRCObjectPool Pool;
    public VRCPlayerApi Owner;

    [UdonSynced, FieldChangeCallback(nameof(NumberOfSneks))]
    public int numberOfSneks;
    public bool GameHasStarted = false;

    // private void OnEnable()
    // {
    //     Owner = Networking.GetOwner(gameObject);
    //     NumberOfSneks = 5;
    //     SpawnSneks();
    //     GameHasStarted = true;
    // }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        Owner = player;
    }

    public void SpawnSneks()
    {
        Owner = Networking.GetOwner(gameObject);
        if (Networking.LocalPlayer == Owner)
        {
            NumberOfSneks = 5;
            for (int i = 0; i < NumberOfSneks; i++)
            {
                Pool.Shuffle();
                Pool.TryToSpawn();
            }
        }
    }

    public int NumberOfSneks
    {
        set
        {
            numberOfSneks = value;
            Debug.Log("NumberOfSneks: " + numberOfSneks);
            if (numberOfSneks <= 0)
            {
                Debug.Log("Spawning Sneks!");
                numberOfSneks = 5;
                SpawnSneks();
            }
        }
        get { return numberOfSneks; }
    }
}
