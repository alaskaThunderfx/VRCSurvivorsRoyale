using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class EffectsContainer : UdonSharpBehaviour
{
    public VRCPlayerApi Owner;
    public PlayerController PlayerController;
    public KnifePool KnifePool;
    public Vector3 Pos;
    public ParticleSystem LevelUpVisual;
    public AudioSource LevelUpAudio;
    public AudioSource Kill;
    // Knife effects
    public ParticleSystem Spark;
    public ParticleSystem Blood;
    public AudioSource Throw;
    public AudioSource KnifeIAOHit;
    public AudioSource KnifeEnemyHit;
    // Fireball effects
    public ParticleSystem Explode;

    public bool IsReady;

    private void Update()
    {
        if (!IsReady)
            return;
        Pos = Owner.GetPosition();
    }

    public void _OnOwnerSet()
    {
        // Set current player to owner
        Owner = Networking.GetOwner(gameObject);
        // Set ParticleSystems and Sounds
        LevelUpVisual = transform.GetChild(0).GetChild(0).GetComponent<ParticleSystem>();
        LevelUpAudio = transform.GetChild(0).GetChild(1).GetComponent<AudioSource>();
        Kill = transform.GetChild(1).GetComponent<AudioSource>();
        Spark = transform.GetChild(2).GetComponent<ParticleSystem>();
        Blood = transform.GetChild(3).GetComponent<ParticleSystem>();
        Throw = transform.GetChild(4).GetComponent<AudioSource>();
        KnifeIAOHit = transform.GetChild(5).GetComponent<AudioSource>();
        KnifeEnemyHit = transform.GetChild(6).GetComponent<AudioSource>();
        Explode = transform.GetChild(7).GetComponent<ParticleSystem>();

        IsReady = true;
    }

    public void LevelUp()
    {
        transform.position = Pos;
        AudioClip bong = LevelUpAudio.clip;
        LevelUpVisual.Play();
        AudioSource.PlayClipAtPoint(bong, Pos);
    }
}
