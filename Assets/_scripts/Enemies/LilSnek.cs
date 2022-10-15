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

    // public HealthBar HealthBar;
    public VRCObjectPool LilSnekPool;

    public LilSnekSpawner LilSnekSpawner;
    public Animator AIAnimator;
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
    public Vector3 CurrentPosition;

    [UdonSynced]
    public float AgentSpeed;
    private bool IsMovingToNext;
    private bool HasSetNextPosition;
    private float InternalWaitTime;
    private float GCTInternalTime;

    private void Start()
    {
        Owner = Networking.GetOwner(gameObject);
        Agent = GetComponent<NavMeshAgent>();
        LocalPlayer = Networking.LocalPlayer;
        HealthBarCanvas = transform.GetChild(16);
        HealthBar = HealthBarCanvas.GetChild(0).GetComponent<HealthBar>();
        LilSnekPool = transform.parent.GetComponent<VRCObjectPool>();
        LilSnekSpawner = transform.parent.GetComponent<LilSnekSpawner>();
        AgentSpeed = Agent.speed;
        WanderIdleTime = Random.Range(0f, 5f);
        InternalWaitTime = WanderIdleTime;
        IsMovingToNext = false;
        GCTInternalTime = GuardChaseTime;
        Health = 3f;
        HealthBar.SetMaxHealth(Health);
    }

    private void OnEnable()
    {
        AIAnimator = GetComponent<Animator>();
        IsSpawning = true;
        SpawnCountdown = 1.1f;
        Owner = Networking.GetOwner(gameObject);
    }

    private void Update()
    {
        // Set animation condisitons
        AIAnimator.SetFloat("Move", AIVelocity);
        AIAnimator.SetBool("Spawn", IsSpawning);
        AIAnimator.SetBool("Dying", IsDying);
        AIAnimator.SetBool("Attack", IsAttacking);

        HealthBarCanvas.LookAt(LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position);

        SpawnCountdown -= Time.deltaTime;
        if (SpawnCountdown <= 0)
        {
            IsSpawning = false;
            Agent.enabled = true;
            Health = 3f;
        }

        if (Agent.enabled)
        {
            AIVelocity = Agent.velocity.magnitude;
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
        }
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

    public float Health
    {
        set
        {
            health = value;
            HealthBar.SetHealth(health);
        }
        get { return health; }
    }
}
