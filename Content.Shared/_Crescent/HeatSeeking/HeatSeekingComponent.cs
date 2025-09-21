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
    public List<SeekerTargets> TargetList = new List<SeekerTargets>();

    [DataField]
    public float RefreshRate = 0.5f; // How often the seeker updates its target in seconds

    public float RefreshTicker;
}

[Serializable, NetSerializable]
public enum GuidanceType
{
    PredictiveGuidance = 1,
    PurePursuit = 2
}

[Serializable]
public class SeekerTargets
{
    public EntityUid Target { get; set; }
    public float Weight { get; set; }
}
