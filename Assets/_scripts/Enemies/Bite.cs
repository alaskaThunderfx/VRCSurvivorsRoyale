
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Bite : UdonSharpBehaviour
{
    public Collider Biter;

    private void OnEnable() {
        Biter = GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider other) {
        if (other.name.Contains("Player")){Debug.Log("Bit ya!");}
    }
}
