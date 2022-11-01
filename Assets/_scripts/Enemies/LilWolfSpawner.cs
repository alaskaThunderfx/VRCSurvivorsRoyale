using UdonSharp;
using UnityEngine;
using UnityEngine.AI;
using VRC.Udon.Common.Interfaces;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon;

public class LilWolfSpawner : UdonSharpBehaviour
{
    public VRCObjectPool Pool;
    public VRCPlayerApi Owner;

    [UdonSynced, FieldChangeCallback(nameof(NumberOfWolfs))]
    public int numberOfWolfs;
    public bool GameHasStarted = false;

    // private void OnEnable()
    // {
    //     Owner = Networking.GetOwner(gameObject);
    //     NumberOfWolfs = 5;
    //     SpawnWolfs();
    //     GameHasStarted = true;
    // }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        Owner = player;
    }

    public void SpawnWolfs()
    {
        Debug.Log("In SpawnWolfs");
        Owner = Networking.GetOwner(gameObject);
        if (Networking.LocalPlayer == Owner)
        {
            NumberOfWolfs = 10;
            for (int i = 0; i < NumberOfWolfs; i++)
            {
                Pool.Shuffle();
                Pool.TryToSpawn();
            }
        }
    }

    public int NumberOfWolfs
    {
        set
        {
            numberOfWolfs = value;
            Debug.Log("NumberOfWolfs: " + numberOfWolfs);
            if (numberOfWolfs <= 0)
            {
                Debug.Log("Spawning Wolfs!");
                numberOfWolfs = 10;
                SpawnWolfs();
            }
        }
        get { return numberOfWolfs; }
    }
}
