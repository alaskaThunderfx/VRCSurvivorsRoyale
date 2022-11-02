using UdonSharp;
using UnityEngine;
using UnityEngine.AI;
using VRC.Udon.Common.Interfaces;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon;

public class LilGhostSpawner : UdonSharpBehaviour
{
    public VRCObjectPool Pool;
    public VRCPlayerApi Owner;

    [UdonSynced, FieldChangeCallback(nameof(NumberOfGhosts))]
    public int numberOfGhosts;
    public bool GameHasStarted = false;

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        Owner = player;
    }

    public void SpawnGhosts()
    {
        Debug.Log("In SpawnGhosts");
        Owner = Networking.GetOwner(gameObject);
        if (Networking.LocalPlayer == Owner)
        {
            NumberOfGhosts = 10;
            for (int i = 0; i < NumberOfGhosts; i++)
            {
                Pool.Shuffle();
                Pool.TryToSpawn();
            }
        }
    }

    public int NumberOfGhosts
    {
        set
        {
            numberOfGhosts = value;
            Debug.Log("NumberOfGhosts: " + numberOfGhosts);
            if (numberOfGhosts <= 0)
            {
                Debug.Log("Spawning Ghosts!");
                numberOfGhosts = 10;
                SpawnGhosts();
            }
        }
        get { return numberOfGhosts; }
    }
}
