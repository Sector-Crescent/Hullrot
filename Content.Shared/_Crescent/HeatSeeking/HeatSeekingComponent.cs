using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using System.Numerics;
namespace Content.Shared._Crescent.HeatSeeking;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HeatSeekingComponent : Component
{
    /// <summary>
    /// How far away can this missile see targets
    /// </summary>
    [DataField]
    public float DefaultSeekingRange = 300f;

    [DataField]
    public Angle WeaponArc = Angle.FromDegrees(360);

    /// <summary>
    /// If null it will default to 100.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Angle? RotationSpeed = 50f;

    /// <summary>
    /// What guidance algorithm should this missile use?
    /// Options are "PredictiveGuidance" and "PurePursuit".
    /// Defaults to "PredictiveGuidance".
    /// </summary>
    [DataField]
    public GuidanceType GuidanceAlgorithm = GuidanceType.PredictiveGuidance;

    /// <summary>
    /// What is this entity targeting?
    /// </summary>
    [DataField]
    public EntityUid? TargetEntity;

    /// <summary>
    /// How fast does the missile accelerate in m/s/s?
    /// </summary>
    [DataField]
    public float Acceleration = 50f;



    /// <summary>
    /// What is the missiles initial speed in m/s?
    /// </summary>
    [DataField]
    public float InitialSpeed = 30f;

    /// <summary>
    /// What is the missiles current speed in m/s?
    /// </summary>
    [DataField]
    public float Speed;

    /// <summary>
    /// What is the missiles field of view in degrees?
    /// </summary>
    [DataField]
    public float FOV = 90f;

    public float oldDistance;

    public Vector2 oldPosition;

    // rework

    [DataField]
    public float StartDelay = 0.5f; // How long before the missile starts moving after being fired

    [DataField]
    public float Fuel = 50f;

    /// <summary>
    /// What is the missiles top speed in m/s?
    /// </summary>
    [DataField]
    public float TopSpeed = 50f;

    [DataField]
    public float BoostAcceleration = 50f;

    [DataField]
    public float CruiseAcceleration = 10f;

    [DataField]
    public float RCSMultiplier = 0.5f; // Reaction Control System multiplier for x axis thrust

    [DataField]
    public SeekerState SeekerState = SeekerState.Idle; // Current state of the seeker

    [DataField]
    public List<SeekerTargets> TargetList = new List<SeekerTargets>();

    [DataField]
    public float RefreshRate = 0.25f; // How often the seeker updates its target in seconds

    public float RefreshTicker;

    public bool Thrusting;

    [DataField]
    public float ProportionalGain = 5f; // Proportional gain for the guidance algorithm

    [DataField]
    public float IntegralGain = 0f; // Integral gain for the guidance algorithm - Integral gain isn't needed since steady state error doesn't exist in our use case

    [DataField]
    public float DerivativeGain = 1f; // Derivative gain for the guidance algorithm

    public Vector2 LastError; // Previous error for the guidance algorithm

    public Vector2 Integral; // Previous integral for the guidance algorithm

    public bool DerivativeInit = false; // Whether the derivative term has been initialized

    [DataField]
    public Vector2? TargetPos = null;

    public bool PositionTracking = true; // Whether the seeker is currently tracking a target position
}

[Serializable, NetSerializable]
public enum GuidanceType
{
    PredictiveGuidance = 1,
    PurePursuit = 2
}

[Serializable, NetSerializable]
public enum SeekerState
{
    Idle = 1,
    Boosting = 2,
    Cruising = 3
}

[Serializable, NetSerializable]
public enum RCSVisualState
{
    BaseLayer,
    Forward,
    Backward,
    Left,
    Right
}

[Serializable]
public class SeekerTargets
{
    public EntityUid Target { get; set; }
    public float Weight { get; set; }
}
