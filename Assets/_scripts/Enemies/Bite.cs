
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Bite : UdonSharpBehaviour
{
    private void OnTriggerEnter(Collider other) {
        Debug.Log("I bit " + other.name);
    }
}
