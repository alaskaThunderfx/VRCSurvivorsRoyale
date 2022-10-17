using UdonSharp;
using UnityEngine;
using UnityEngine.AI;
using VRC.Udon.Common.Interfaces;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon;

public class LilSnek : UdonSharpBehaviour
{
    [Header("Components")]
    public NavMeshAgent Agent;
    public VRCPlayerApi Owner;
    private VRCPlayerApi LocalPlayer;
    public VRCObjectPool LilSnekPool;

    public Cyan.PlayerObjectPool.CyanPlayerObjectAssigner PlayerObjectAssigner;
    public PlayerController PlayerController;

    public LilSnekSpawner LilSnekSpawner;
    public Animator AIAnimator;
    float DeathScene;
    public Transform HealthBarCanvas;
    public HealthBar HealthBar;

    [Header("Stats")]
    [UdonSynced, FieldChangeCallback(nameof(Health))]
    public float health;
    public float Experience = 1f;
    public float MaxWanderDistance = 5f;
    public float WanderIdleTime;

    public float GuardChaseTime = 15f;

    [UdonSynced]
    public bool IsSpawning;
    public float SpawnCountdown = 1.1f;

    [UdonSynced]
    public bool IsDying;

    [UdonSynced]
    public bool IsAttacking;

    [UdonSynced]
    public Vector3 CurrentDestination;

    [UdonSynced]
    public float AIVelocity;

    [UdonSynced]
    public int CurrentState;

    [UdonSynced]
    public float AgentSpeed;
    private bool IsMovingToNext;
    private bool HasSetNextPosition;
    private float InternalWaitTime;
    private float GCTInternalTime;
    private float AttackCD;
    private float AnimCD;
    public GameObject bite;

    private void OnEnable()
    {
        Owner = Networking.GetOwner(gameObject);

        LocalPlayer = Networking.LocalPlayer;
        Agent = GetComponent<NavMeshAgent>();
        Agent.enabled = true;
        // Agent.updatePosition = false;
        CurrentState = 0;

        HealthBarCanvas = transform.GetChild(16);
        HealthBar = HealthBarCanvas.GetChild(0).GetComponent<HealthBar>();

        if (gameObject.name != "TestSnake")
        {
            LilSnekPool = transform.parent.GetComponent<VRCObjectPool>();
            LilSnekSpawner = transform.parent.GetComponent<LilSnekSpawner>();
        }
        else
        {
            LilSnekPool = GameObject.Find("SnakeSpawner").GetComponent<VRCObjectPool>();
            LilSnekSpawner = GameObject.Find("SnakeSpawner").GetComponent<LilSnekSpawner>();
        }

        if (Networking.LocalPlayer == Owner)
            AgentSpeed = Agent.speed;
        WanderIdleTime = Random.Range(0f, 5f);
        InternalWaitTime = WanderIdleTime;
        IsMovingToNext = false;
        GCTInternalTime = GuardChaseTime;
        Health = 3f;
        HealthBar.SetMaxHealth(Health);
        PlayerObjectAssigner = GameObject
            .Find("PlayerObjectAssigner")
            .GetComponent<Cyan.PlayerObjectPool.CyanPlayerObjectAssigner>();
        AIAnimator = GetComponent<Animator>();
        DeathScene = 1.3f;
        if (Networking.LocalPlayer == Owner)
            IsSpawning = true;
        SpawnCountdown = 1.1f;
        IsDying = false;
        AnimCD = .2f;
    }

    private void Update()
    {
        // Set animation conditions
        AIAnimator.SetFloat("Move", AIVelocity);
        AIAnimator.SetBool("Spawn", IsSpawning);
        AIAnimator.SetBool("Dying", IsDying);
        AIAnimator.SetBool("Attack", IsAttacking);

        // make sure to stop the update loop if they are dying
        if (IsDying)
        {
            DeathScene -= Time.deltaTime;

            if (DeathScene <= 0)
            {
                Debug.Log(Owner.playerId);
                if (Networking.LocalPlayer == Owner)
                {
                    Debug.Log("You're the owner" + Owner.playerId);
                    LilSnekPool.Return(gameObject);
                    LilSnekSpawner.NumberOfSneks -= 1;
                }
            }
        }

        HealthBarCanvas.LookAt(
            LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position
        );

        SpawnCountdown -= Time.deltaTime;
        if (SpawnCountdown <= 0 && IsSpawning)
        {
            IsSpawning = false;
            Agent.enabled = true;
            Health = 3f;
        }

        if (Agent.enabled)
        {
            gameObject.GetComponent<Collider>().enabled = true;
            if (Networking.LocalPlayer != Owner)
                Agent.SetDestination(CurrentDestination);
            else
                AIVelocity = Agent.velocity.magnitude;

            switch (CurrentState)
            {
                case 0:
                    if (!Networking.IsOwner(gameObject))
                        break;
                    if (!IsMovingToNext)
                    {
                        HasSetNextPosition = false;
                        InternalWaitTime -= Time.deltaTime;
                        if (InternalWaitTime < 0f)
                        {
                            WanderIdleTime = Random.Range(0f, 5f);
                            IsMovingToNext = true;
                            InternalWaitTime = WanderIdleTime;
                            StartWandering();
                        }
                    }

                    if (Agent.remainingDistance < 1 && HasSetNextPosition)
                    {
                        IsMovingToNext = false;
                        WanderIdleTime = Random.Range(0f, 5f);
                        InternalWaitTime = WanderIdleTime;
                    }
                    break;
                case 1:
                    if (!Networking.IsOwner(gameObject))
                        break;
                    transform.GetChild(17).GetComponent<Collider>().enabled = false;
                    SetAIDestination(Owner.GetPosition());
                    GCTInternalTime -= Time.deltaTime;
                    if (Agent.remainingDistance < 1.5f)
                    {
                        AttackCD -= Time.deltaTime;
                        if (AttackCD <= 0)
                        {
                            AIAnimator.SetTrigger("Attacks");
                        }
                    }
                    if (GCTInternalTime < 0f)
                    {
                        CurrentState = 0;
                        GCTInternalTime = GuardChaseTime;
                        transform.GetChild(17).GetComponent<Collider>().enabled = true;
                    }
                    break;
            }
        }
    }

    private void OnDisable()
    {
        if (Networking.LocalPlayer == Owner)
            IsDying = false;
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        Owner = player;
    }

    public void StartWandering()
    {
        SetAIDestination(CalculateRandomPosition(MaxWanderDistance));
    }

    public void SetAIDestination(Vector3 Target)
    {
        HasSetNextPosition = true;
        CurrentDestination = Target;
        Agent.SetDestination(CurrentDestination);
    }

    private Vector3 CalculateRandomPosition(float dist)
    {
        var randDir = transform.position + Random.insideUnitSphere * dist;
        NavMeshHit hit;
        NavMesh.SamplePosition(randDir, out hit, dist, NavMesh.AllAreas);
        return hit.position;
    }

    public void activateBite(){
        bite.GetComponent<Collider>().enabled = true;
    }

    public void deactivateBite()
    {
        bite.GetComponent<Collider>().enabled = false;
    }

    public float Health
    {
        set
        {
            health = value;
            HealthBar.SetHealth(health);
            if (health <= 0)
            {
                PlayerController = PlayerObjectAssigner
                    ._GetPlayerPooledObject(Owner)
                    .GetComponent<PlayerController>();
                PlayerController.Experience += Experience;
                Debug.Log("Player: " + PlayerController.Owner);
                Debug.Log("Their total experience: " + PlayerController.Experience);
                AudioSource.PlayClipAtPoint(
                    PlayerController.EffectsContainer.Kill.clip,
                    transform.position
                );
                Networking.SetOwner(Owner, LilSnekPool.gameObject);
                Agent.enabled = false;
                // Agent.SetDestination(transform.position);
                gameObject.GetComponent<Collider>().enabled = false;
                if (Networking.LocalPlayer == Owner)
                    IsDying = true;
            }
        }
        get { return health; }
    }
}
