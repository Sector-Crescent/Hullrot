using Content.Shared._Crescent.HeatSeeking;
using Content.Shared.Cargo.Events;
using Content.Shared.Chat;
using Content.Shared.Coordinates;
using Content.Shared.Interaction;
using Content.Shared.Projectiles;
using Content.Shared.Shuttles.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.AccessControl;

namespace Content.Server._Crescent.HeatSeeking;

/// <summary>
/// This handles...
/// </summary>
public sealed class HeatSeekingSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RotateToFaceSystem _rotate = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HeatSeekingComponent, TransformComponent>(); // get all heat seeking missiles
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            comp.SeekerState = SeekerState.Idle;
            if (!TryComp<PhysicsComponent>(uid, out var physics))
                continue;
            if (comp.StartDelay >= 0f) comp.StartDelay -= frameTime; // reduce start delay
            else if (comp.Fuel > 0f)
            {
                float accelModifier = comp.CruiseAcceleration;

                if (comp.SeekerState == SeekerState.Boosting) // if the missile is boosting, apply boost acceleration
                {
                    accelModifier = comp.BoostAcceleration;
                    comp.PositionTracking = false;
                }
                else if (comp.SeekerState == SeekerState.Cruising) // if the missile is cruising, apply cruise acceleration
                {
                    accelModifier = comp.CruiseAcceleration;
                    comp.PositionTracking = false;
                }
                else if (comp.SeekerState == SeekerState.Idle) // if the missile is idle, switch to position tracking
                {
                    accelModifier = comp.BoostAcceleration;
                    if (TryComp<ProjectileComponent>(uid, out var projectile))//&& comp.TargetPos == null
                        comp.TargetPos = _transform.ToMapCoordinates(Transform(projectile.Shooter ?? uid).Coordinates).Position + new Vector2(0f, 2f);

                    comp.PositionTracking = true; // set position tracking to true
                }
                float angleDegrees = 0f; // the angle between the current velocity vector and the target velocity vector
                if (comp.PositionTracking) // if the missile is tracking a position, set the target linear speed to the distance to the target
                {
                    Vector2 targetVector = PID(frameTime, comp.TargetPos ?? new Vector2(0f, 0f), _transform.ToMapCoordinates(xform.Coordinates).Position, comp); // get the target vector using PID controller
                    targetVector = Vector2.Clamp(targetVector, targetVector.Normalized() * 0f, targetVector.Normalized() * comp.TopSpeed); // clamp the target vector to the top speed
                    angleDegrees = MathF.Atan2(_transform.GetWorldRotation(uid).ToWorldVec().Normalized().Y, _transform.GetWorldRotation(uid).ToWorldVec().Normalized().X) - MathF.Atan2(targetVector.Normalized().Y, targetVector.Normalized().X);
                    if (angleDegrees < 0) angleDegrees += 2 * MathF.PI;
                    //angleDegrees = MathF.Acos(Vector2.Dot(_transform.GetWorldRotation(uid).ToWorldVec().Normalized(), targetVector.Normalized())); // the angle between two vectors is equal to Acos of their dot product
                    Vector2 y = _transform.GetWorldRotation(uid).ToWorldVec().Normalized() * (targetVector.Length() * MathF.Cos(angleDegrees)); // the y component of the target vector
                    Vector2 x = (_transform.GetWorldRotation(uid) + MathHelper.DegreesToRadians(90)).ToWorldVec().Normalized() * (targetVector.Length() * MathF.Sin(angleDegrees)); // the x component of the target vector
                    Log.Warning($"current position: {_transform.ToMapCoordinates(xform.Coordinates).Position}. target position: {comp.TargetPos}. target vector: {targetVector}, normalized {targetVector.Normalized()}. angleDegrees: {angleDegrees}");

                    comp.Fuel -= frameTime;

                    Log.Warning($"current vector: {_transform.GetWorldRotation(uid).ToWorldVec().Normalized()}, rotated: {(_transform.GetWorldRotation(uid) - MathHelper.DegreesToRadians(90)).ToWorldVec().Normalized()}. target x: {-x}. target y: {y}");
                    if (y.Length() >= 5f) // make it boost on/off because thats cool and has aura
                    {
                        _physics.ApplyLinearImpulse(uid, y * physics.Mass * frameTime * accelModifier);
                        if (angleDegrees >= MathF.PI * 1.5f || angleDegrees <= MathF.PI * 0.5f)
                        {
                            _appearance.SetData(uid, RCSVisualState.Forward, true);
                            _appearance.SetData(uid, RCSVisualState.Backward, false);
                        }
                        else
                        {
                            _appearance.SetData(uid, RCSVisualState.Forward, false);
                            _appearance.SetData(uid, RCSVisualState.Backward, true);
                        }
                    }
                    else
                    {
                        _appearance.SetData(uid, RCSVisualState.Forward, false);
                        _appearance.SetData(uid, RCSVisualState.Backward, false);
                    }
                    if (x.Length() >= 5f)
                    {
                        _physics.ApplyLinearImpulse(uid, x * physics.Mass * frameTime * accelModifier);
                        if (angleDegrees >= MathF.PI * 1.5f || angleDegrees <= MathF.PI * 0.5f)
                        {
                            _appearance.SetData(uid, RCSVisualState.Left, true);
                            _appearance.SetData(uid, RCSVisualState.Right, false);
                        }
                        else
                        {
                            _appearance.SetData(uid, RCSVisualState.Left, false);
                            _appearance.SetData(uid, RCSVisualState.Right, true);
                        }
                    }
                    else
                    {
                        _appearance.SetData(uid, RCSVisualState.Left, false);
                        _appearance.SetData(uid, RCSVisualState.Right, false);
                    }

                    if (!comp.Thrusting)
                    {
                        comp.Thrusting = true;
                        // thrust changed state, play sounds and stuff
                    }
                }
                else // if the missile is not tracking a position, set the target linear speed to the top speed. this is just to make it look more like a missile lol
                {
                    Vector2 targetVector = _transform.GetWorldRotation(uid).ToWorldVec().Normalized() * comp.TopSpeed; // get the target vector
                    targetVector = Vector2.Clamp(targetVector, targetVector.Normalized() * 0f, targetVector.Normalized() * comp.TopSpeed); // clamp the target vector to the top speed
                    angleDegrees = MathF.Acos(Vector2.Dot(_transform.GetWorldRotation(uid).ToWorldVec().Normalized(), physics.LinearVelocity.Normalized())) * MathF.PI / 180f; // the angle between two vectors is equal to Acos of their dot product
                    Vector2 x = targetVector * MathF.Cos(angleDegrees); // the x component of the target vector
                    Vector2 y = targetVector * MathF.Sin(angleDegrees); // the y component of the target vector
                    Log.Warning($"velocity vector: {physics.LinearVelocity}. current vector: {_transform.ToMapCoordinates(xform.Coordinates).Position}. target vector {comp.TargetPos}. vertical vector: {_transform.GetWorldRotation(uid).ToWorldVec().Normalized()}.");
                    // DIAGNOSE WHY MISSILE MOVES IN THE WRONG DIRECION
                    comp.Fuel -= frameTime;
                    _physics.ApplyLinearImpulse(uid, Vector2.Clamp(x, x.Normalized() * -accelModifier, x.Normalized() * accelModifier) * physics.Mass * frameTime);
                    _physics.ApplyLinearImpulse(uid, Vector2.Clamp(y, y.Normalized() * -accelModifier, y.Normalized() * accelModifier) * physics.Mass * frameTime);
                }
            }
            //if (physics.LinearVelocity.Length() <= comp.TopSpeed * 0.7f)
            //    comp.SeekerState = SeekerState.Boosting; // boost the missile when initially launched to kick it off from the ship
            //else
            //    comp.SeekerState = SeekerState.Cruising; // cruise the missile when it has reached 70% of its top speed

            if (comp.TargetEntity.HasValue) // if the missile has a target, run its guidance algorithm
            {
                if (comp.GuidanceAlgorithm == GuidanceType.PredictiveGuidance)
                    PredictiveGuidance(uid, comp, Transform(uid), frameTime);
                else
                    PurePursuit(uid, comp, Transform(uid), frameTime);
            }
            else
            {
                comp.SeekerState = SeekerState.Idle; // seeker has no target
            }
        }
    }

    public void HorizontalThrust(EntityUid uid, HeatSeekingComponent comp, float thrust, float ft) // apply horizontal thrust to the missile
    {
        if (!TryComp<PhysicsComponent>(uid, out var physics))
            return;
        _physics.ApplyLinearImpulse(uid, (_transform.GetWorldRotation(uid) - MathHelper.DegreesToRadians(90)).ToWorldVec() * thrust * physics.Mass * ft);
        comp.Thrusting = true; // set thrusting to true
        bool left = 0f < thrust;
    }

    public void VerticalThrust(EntityUid uid, HeatSeekingComponent comp, float thrust, float ft) // apply vertical thrust to the missile
    {
        if (!TryComp<PhysicsComponent>(uid, out var physics))
            return;
        _physics.ApplyLinearImpulse(uid, _transform.GetWorldRotation(uid).ToWorldVec().Normalized() * thrust * physics.Mass * ft);
        comp.Thrusting = true; // set thrusting to true
        bool forward = 0f < thrust;
    }

    /// <summary>
    /// PID controller for the missile guidance.
    /// </summary>
    /// <param name="ft">Frame Time</param>
    /// <param name="target">The target vector</param>
    /// <param name="current">The current vector</param>
    /// <param name="comp"> The heat seeking component containing the PID parameters</param>
    public Vector2 PID(float ft, Vector2 target, Vector2 current, HeatSeekingComponent comp)
    {
        Vector2 deltaError;
        Vector2 error = target - current; // calculate the error between the target and current vectors
        if (comp.DerivativeInit)
        {
            deltaError = (error - comp.LastError) / ft; // change in error over time
            comp.LastError = error; // update the last error
        }
        else
        {
            deltaError = new Vector2(0f, 0f);
            comp.LastError = error; // update the last error
            comp.DerivativeInit = true; // set the derivative term as initialized
        }
        comp.Integral += error * ft; // integral term
        comp.Integral = Vector2.Clamp(comp.Integral, comp.Integral.Normalized() * 0f, comp.Integral.Normalized() * 2f); // clamp the integral term to prevent windup

        Vector2 p = comp.ProportionalGain * error; // proportional term
        Vector2 i = comp.IntegralGain * comp.Integral; // calculate the integral term
        Vector2 d = comp.DerivativeGain * deltaError; // calculate the derivative term

        return p + i + d; // return the PID output
    }

    //private void ThrustForward(EntityUid uid, float speed)
    //{
    //    if (!TryComp<PhysicsComponent>(uid, out var physics))
    //        return;
    //    if (!TryComp<HeatSeekingComponent>(uid, out var seeker))
    //        return;
    //    _physics.ApplyLinearImpulse(uid, _transform.GetWorldRotation(uid).ToWorldVec().Normalized() * speed);
    //    if (TryComp<SharedPointLightComponent>(uid, out var light))
    //    {
    //        _pointLight.SetEnabled(uid, true, light);
    //    }
    //    if (TryComp<AppearanceComponent>(uid, out var appearance))
    //    {
    //        _appearance.SetData(uid, HeatSeekerVisuals.Slots, HeatSeekerThrustState.Forward);
    //    }
    //}
    public void PredictiveGuidance(EntityUid uid, HeatSeekingComponent comp, TransformComponent xform, float frameTime) // Predictive Guidance, predicts targets position at impact time.
    {
        if (!comp.TargetEntity.HasValue)
            return;

        float oldDistance = comp.oldDistance;
        var entXform = Transform(comp.TargetEntity.Value); // get target transform
        var distance = Vector2.Distance(
            _transform.ToMapCoordinates(xform.Coordinates).Position,
            _transform.ToMapCoordinates(entXform.Coordinates).Position
        ); // current distance from target
        var angle = (
            _transform.ToMapCoordinates(entXform.Coordinates).Position -
            _transform.ToMapCoordinates(xform.Coordinates).Position
        ).ToWorldAngle(); // current angle towards target

        if (angle > Angle.ShortestDistance(angle, _transform.GetWorldRotation(xform) + MathHelper.DegreesToRadians(comp.FOV))
        || angle < Angle.ShortestDistance(angle, _transform.GetWorldRotation(xform) - MathHelper.DegreesToRadians(comp.FOV))) // if missile missed then lose lock.
        {
            comp.TargetEntity = null;
            return;
        }
        Log.Warning($"target entity: {comp.TargetEntity}. angle to target: {angle}. missile angle: {_transform.GetWorldRotation(xform)}. high/low FOV limits: {Angle.ShortestDistance(angle, _transform.GetWorldRotation(xform) + MathHelper.DegreesToRadians(comp.FOV))}, {Angle.ShortestDistance(angle, _transform.GetWorldRotation(xform) - MathHelper.DegreesToRadians(comp.FOV))} ");

        var targetVelocity = _transform.ToMapCoordinates(entXform.Coordinates).Position - comp.oldPosition; // get target velocity
        float timeToImpact = distance / (oldDistance - distance); // time it will take for the missile to reach the target
        if (timeToImpact < 0.1) { timeToImpact = 0.1f; } // prevent negative time to impact, that messes up guidance
        var predictedPosition = _transform.ToMapCoordinates(entXform.Coordinates).Position + (targetVelocity * timeToImpact); // predict target position at impact time

        Angle targetAngle = (predictedPosition - _transform.ToMapCoordinates(xform.Coordinates).Position).ToWorldAngle(); // the angle the missile will try to face

        if (comp.SeekerState != SeekerState.Cruising)
            _rotate.TryRotateTo(uid, targetAngle, frameTime, comp.WeaponArc, comp.RotationSpeed?.Theta * 2 ?? double.MaxValue, xform);
        else
            _rotate.TryRotateTo(uid, targetAngle, frameTime, comp.WeaponArc, comp.RotationSpeed?.Theta ?? double.MaxValue, xform); // rotate towards target angle

        comp.oldPosition = _transform.ToMapCoordinates(entXform.Coordinates).Position;
        comp.oldDistance = distance;
    }

    public void PurePursuit(EntityUid uid, HeatSeekingComponent comp, TransformComponent xform, float frameTime) // Pure Pursuit, points directly at target.
    {
        if (comp.TargetEntity.HasValue)
        {
            var entXform = Transform(comp.TargetEntity.Value); // get target transform
            var originalAngle = _transform.GetWorldRotation(xform); // get current angle of missile

            var angle = (
                _transform.ToMapCoordinates(entXform.Coordinates).Position -
                _transform.ToMapCoordinates(xform.Coordinates).Position
            ).ToWorldAngle(); // current angle towards target

            _rotate.TryRotateTo(uid, angle, frameTime, comp.WeaponArc, comp.RotationSpeed?.Theta ?? double.MaxValue, xform); // rotate towards target angle
        }
    }
}
