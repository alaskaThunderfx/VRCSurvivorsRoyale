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
    public Burner Burner;

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

    public ParticleSystem Explode;

    public GameObject Burn;
    public float BurnTime;
    public bool IsBurning;

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
        Burn = transform.GetChild(2).gameObject;
        BurnTime = FireballPool.BurnTime;
        Burner = transform.GetChild(2).GetComponent<Burner>();
        Burner._OnOwnerSet();

        PlayerController = FireballPool.PlayerController;
        EffectsContainer = FireballPool.EffectsContainer;
        Explode = EffectsContainer.Explode;
        // HitIAOSound = EffectsContainer.IAOHit.clip;
        // HitEnemySound = EffectsContainer.EnemyHit.clip;
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

        Vector3 PlayerPos = FireballPool.PlayerPosition;
        FireballPosition = new Vector3(PlayerPos.x, PlayerPos.y + .2f, PlayerPos.z);
        FireballRotation = FireballPool.PlayerRotation;

        transform.SetPositionAndRotation(FireballPosition, FireballRotation);
        FireballRB.velocity = (transform.forward + transform.up) * FireballPool.Force;

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
        // AudioSource.PlayClipAtPoint(Throw, transform.position, 0.1f);
    }

    private void Update()
    {
        if (IsBurning)
        {
            BurnTime -= Time.deltaTime;
            if (BurnTime <= 0)
            {
                BurnTime = FireballPool.BurnTime;
                gameObject.SetActive(false);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        EffectPosition = transform.position;
        if (other.name.Contains("Floor"))
        {
            FireballRB.isKinematic = true;
            HitIAO = true;
            IsBurning = true;
            Explode.transform.position = transform.position;
            Explode.Play(true);
            Burn.SetActive(true);
        }
    }

    private void OnDisable()
    {
        if (ReadyToGo)
        { 
            // Reset conditions here
            HitIAO = false;
            IsBurning = false;
            FireballRB.isKinematic = false;
            Burn.SetActive(false);
            mesh.localScale = OgMesh;
            particle.localScale = OgParticle;
        }

        if (HitIAO)
        {
            // AudioSource.PlayClipAtPoint(HitIAOSound, transform.position, 0.1f);
            Explode.transform.position = transform.position;
            Explode.Play(true);
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
