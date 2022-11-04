using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Fireball : UdonSharpBehaviour
{
    [Header("Components")]
    public VRCPlayerApi Owner;
    public PlayerController PlayerController;
    public EffectsContainer EffectsContainer;
    public FireballPool FireballPool;
    public Rigidbody FireballRB;

    [Header("Fireball Info")]
    // Used to initialize where the Fireball will spawn in reference to the current player
    public Vector3 FireballPosition;
    public Quaternion FireballRotation;

    // To store Enemy hit info
    public Collider Enemy;

    // For tracking distance
    public float DistanceTravelled;

    [Header("Sounds and VFX")]
    // Used to detect if what was collided with was an enemy
    public bool HitEnemy;

    // Checks to see if what was hit was an inanimate object
    public bool HitIAO;

    // The effect when the Fireball hits an IAO
    public ParticleSystem Spark;

    // The effect for when a Fireball hits an enemy
    public ParticleSystem Blood;

    // The sound made when the player throws the Fireball
    private AudioClip Throw;

    // The sound when a Fireball hits an IAO
    private AudioClip HitIAOSound;

    // The sound when a Fireball hits an enemy
    private AudioClip HitEnemySound;

    // The sound made by the enemy when killed
    private AudioClip Kill;

    // The position to player the effects at
    public Vector3 EffectPosition;

    // for OnEnable functionality
    public bool ReadyToGo = false;
    public Transform mesh;
    public Transform particle;
    public Vector3 OgMesh;
    public Vector3 OgParticle;
    public bool HasStarted;

    // Ran after the owner has been set on this object
    public void _OnOwnerSet()
    {
        Debug.Log("In _OnOwnerSet in Fireball");
        // Set AudioClips
        Owner = Networking.GetOwner(gameObject);
        FireballPool = transform.parent.GetComponent<FireballPool>();

        PlayerController = FireballPool.PlayerController;
        EffectsContainer = FireballPool.EffectsContainer;
        Spark = EffectsContainer.Spark;
        Blood = EffectsContainer.Blood;
        Throw = EffectsContainer.Throw.clip;
        HitIAOSound = EffectsContainer.IAOHit.clip;
        HitEnemySound = EffectsContainer.EnemyHit.clip;
        Kill = EffectsContainer.Kill.clip;
        gameObject.SetActive(false);
    }

    // When it's the Fireball's turn to be thrown
    private void OnEnable()
    {
        if (ReadyToGo == false)
        {
            return;
        }
        FireballPool = transform.parent.GetComponent<FireballPool>();
        FireballRB = transform.GetComponent<Rigidbody>();
        DistanceTravelled = 0;
        HitEnemy = false;
        HitIAO = false;

        FireballPosition = new Vector3(
            Random.Range(FireballPool.PlayerPosition.x - .2f, FireballPool.PlayerPosition.x + .2f),
            Random.Range(.8f, 1.2f),
            FireballPool.PlayerPosition.z
        );
        FireballRotation = FireballPool.PlayerRotation;

        transform.SetPositionAndRotation(FireballPosition, FireballRotation);
        FireballRB.velocity = transform.forward * FireballPool.Force;

        mesh = transform.GetChild(0);
        particle = transform.GetChild(1);
        OgMesh = mesh.localScale;
        OgParticle = particle.localScale;
        mesh.localScale = new Vector3(
            OgMesh.x * FireballPool.Size,
            OgMesh.y * FireballPool.Size,
            OgMesh.z * FireballPool.Size
        );
        particle.localScale = new Vector3(
            OgParticle.x * FireballPool.Size,
            OgParticle.y * FireballPool.Size,
            OgParticle.z * FireballPool.Size
        );
        AudioSource.PlayClipAtPoint(Throw, transform.position, 0.1f);
    }

    private void Update()
    {
        if (ReadyToGo == false)
        {
            return;
        }
        // Tracking Distance
        DistanceTravelled += Vector3.Distance(transform.position, FireballPosition);
        if (DistanceTravelled >= FireballPool.Range)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        EffectPosition = transform.position;
        if (other.name.Contains("IAE"))
        {
            Debug.Log("Hit an inanimate object");
            HitIAO = true;
            gameObject.SetActive(false);
        }
        else if (other.name.Contains("Enemy"))
        {
            Debug.Log(Owner + " hit a " + other.name + "!");
            HitEnemy = true;
            Enemy = other;
            AudioSource.PlayClipAtPoint(HitEnemySound, transform.position);
            Blood.transform.position = transform.position;
            Blood.Play(true);
            Networking.SetOwner(Owner, other.gameObject);
            gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (ReadyToGo)
        {
            mesh.localScale = OgMesh;
            particle.localScale = OgParticle;
        }

        if (HitIAO)
        {
            AudioSource.PlayClipAtPoint(HitIAOSound, transform.position, 0.1f);
            Spark.transform.position = transform.position;
            Spark.Play(true);
        }

        ReadyToGo = true;
        if (HitEnemy && Networking.LocalPlayer == Owner)
        {
            if (Enemy.name.Contains("LilSnek"))
            {
                LilSnek LilSnek = Enemy.GetComponent<LilSnek>();
                LilSnek.Health -= FireballPool.Damage;
            }
            else if (Enemy.name.Contains("LilWolf"))
            {
                LilWolf LilWolf = Enemy.GetComponent<LilWolf>();
                LilWolf.Health -= FireballPool.Damage;
            }
            else if (Enemy.name.Contains("LilGhost"))
            {
                LilGhost LilGhost = Enemy.GetComponent<LilGhost>();
                LilGhost.Health -= FireballPool.Damage;
            }
            HitEnemy = false;
        }
    }

    public void EnemyInteraction(Collider Enemy) { }
}
