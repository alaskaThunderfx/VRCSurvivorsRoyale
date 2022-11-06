using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Burner : UdonSharpBehaviour
{
    public GameObject BurnedObject;
    public float BurnTime;
    public bool IsReady;
    public VRCPlayerApi Owner;
    public Collider BurnTrigger;
    public Fireball Fireball;

    public void _OnOwnerSet()
    {
        Fireball = transform.parent.GetComponent<Fireball>();
        Owner = Fireball.Owner;
        BurnTrigger = GetComponent<Collider>();
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (IsReady)
        {
            BurnTime = Fireball.BurnTime;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.name.Contains("Enemy"))
        {
            string EnemyName = other.name;
            Debug.Log(EnemyName);
        }
    }

    private void OnDisable()
    {
        if (IsReady) { }
        else
        {
            IsReady = true;
        }
    }
}
