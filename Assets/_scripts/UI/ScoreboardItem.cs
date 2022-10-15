
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class ScoreboardItem : UdonSharpBehaviour
{
    public Text UsernameText;
    public Text ExperienceText;
    public Text Rank;

    public void Initialize(int rank, VRCPlayerApi player, float exp)
    {
        Rank.text = rank.ToString();
        UsernameText.text = player.displayName;
        ExperienceText.text = exp.ToString();
    }
}
