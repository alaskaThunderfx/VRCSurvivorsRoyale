using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon.Common.Interfaces;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon;

public class Scoreboard : UdonSharpBehaviour
{
    [SerializeField] Transform Container;
    [SerializeField] GameObject ScoreboardItemPrefab;
    public Cyan.PlayerObjectPool.CyanPlayerObjectAssigner PlayersPool;
    public Component[] Players;

    public void UpdateBoard()
    {
        Debug.Log("In Scoreboard > UpdateBoard()");
        foreach (Transform child in transform)
        {
            Debug.Log("Destroying Row");
            Destroy(child.gameObject);
        }
        Debug.Log("Beginning SortPlayers() from UpdateBoard()");
        SortPlayers();
        Debug.Log("Finished SortPlayers(), back in UpdateBoard()");
        int rank = 1;
        foreach (Component player in Players)
        {
            Debug.Log("In the forach loop for Players");
            VRCPlayerApi p = Networking.GetOwner(player.gameObject);
            float xp = player.GetComponent<PlayerController>().Experience;
            Debug.Log("About to AddScoreboardItem()");
            Debug.Log(rank);
            Debug.Log(p.displayName);
            Debug.Log(xp);
            AddScoreboardItem(rank, p, xp);
            rank++;
        }
    }

    public void SortPlayers()
    {
        Debug.Log("In Scoreboard > SortPlayers()");
        Players = PlayersPool._GetActivePoolObjects();
        Debug.Log("The length of the Players array is " + Players.Length);
        for (int i = 0; i < Players.Length; i++)
        {
            Debug.Log("In i loop");
            for (int j = 0; j < Players.Length - 1; j++)
            {
                Debug.Log("Looping through Array. i = " + i + "j = " + j);
                if (
                    Players[j].GetComponent<PlayerController>().Experience
                    < Players[j + 1].GetComponent<PlayerController>().Experience
                )
                {
                    Component temp = Players[j];
                    Players[j] = Players[j + 1];
                    Players[j + 1] = temp;
                }
            }
        }
    }

    public void AddScoreboardItem(int rank, VRCPlayerApi player, float exp)
    {
        Debug.Log("In AddScoreboardItem()");
        ScoreboardItem row = Instantiate(ScoreboardItemPrefab, Container).GetComponent<ScoreboardItem>();
        row.Initialize(rank, player, (float)Math.Round(exp, 0));
        Debug.Log("Finished AddScoreboardItem()");
    }
}
