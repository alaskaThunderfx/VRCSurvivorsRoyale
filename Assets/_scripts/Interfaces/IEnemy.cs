using UdonSharp;
using UnityEngine;
using UnityEngine.AI;
using VRC.Udon.Common.Interfaces;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon;

public interface IEnemy
{
    // Components and associated variables that store relevant info for it
    NavMeshAgent Agent { get; set; }
    VRCPlayerApi Owner { get; set; }
    VRCPlayerApi LocalPlayer { get; set; }
    Cyan.PlayerObjectPool.CyanPlayerObjectAssigner PlayerObjectAssigner { get; set; }
    PlayerController PlayerController { get; set; }
    Animator AIAnimator { get; set; }
    float DeathScene { get; set; }
    Transform HealthBarCanvas { get; set; }
    HealthBar HealthBar { get; set; }
    Collider Biter { get; set; }

    // Enemy stats
    float health { get; set; }
    float DMG { get; set; }
    float Experience { get; set; }
    float MaxWanderDistance { get; set; }
    float WanderIdleTime { get; set; }
    float GuardChaseTime { get; set; }

    // Variables for controller animation, collider states and for syncing location
    bool IsSpawning { get; set; }
    float SpawnCountdown { get; set; }
    bool IsDying { get; set; }
    bool IsAttacking { get; set; }
    Vector3 CurrentDestination { get; set; }
    float AIVelocity { get; set; }
    int CurrentState { get; set; }
    float AgentSpeed { get; set; }
    bool IsMovingToNext { get; set; }
    bool HasSetNextPosition { get; set; }
    float InternalWaitTime { get; set; }
    float GCTInternalTime { get; set; }
    float AttackCD { get; set; }
}
