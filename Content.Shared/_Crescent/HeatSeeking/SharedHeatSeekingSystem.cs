using Content.Shared._Crescent.HeatSeeking;
using Content.Shared.Interaction;
using Content.Shared.Projectiles;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using System.Linq;
using System.Numerics;

namespace Content.Server._Crescent.HeatSeeking;

/// <summary>
/// This handles...
/// </summary>
public sealed class HeatSeekingSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RotateToFaceSystem _rotate = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HeatSeekingComponent, TransformComponent>(); // get all heat seeking missiles
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!TryComp<PhysicsComponent>(uid, out var physics))
                continue;
            if (comp.StartDelay > 0f) comp.StartDelay -= frameTime;
            else if (comp.Fuel > 0f)
            {
                if (comp.Speed < comp.TopSpeed)
                    comp.Speed = physics.LinearVelocity.Length() + comp.Acceleration * frameTime;
                _physics.SetLinearVelocity(uid, _transform.GetWorldRotation(xform).ToWorldVec().Normalized() * comp.Speed);
                comp.Fuel -= frameTime;
            }
            if (comp.RefreshTicker > 0f) { comp.RefreshTicker -= frameTime; }
            else
            {
                RefreshTargetList(uid, comp, xform);
                comp.RefreshTicker = comp.RefreshRate;
            }
            if (comp.TargetEntity.HasValue) // if the missile has a target, run its guidance algorithm
            {
                if (comp.GuidanceAlgorithm == GuidanceType.PredictiveGuidance)
                    PredictiveGuidance(uid, comp, Transform(uid), frameTime);
                else
                    PurePursuit(uid, comp, Transform(uid), frameTime);
            }
        }
    }

    public void RefreshTargetList(EntityUid uid, HeatSeekingComponent comp, TransformComponent xform) // refreshes the list of potential targets
    {
        comp.TargetList.Clear();
        var shipQuery = EntityQueryEnumerator<CanBeHeatTrackedComponent, TransformComponent>(); // get all entities that can be tracked
        while (shipQuery.MoveNext(out var tUid, out var tComp, out var tXform))
        {
            var angle = (
                _transform.ToMapCoordinates(tXform.Coordinates).Position -
                _transform.ToMapCoordinates(xform.Coordinates).Position
            ).ToWorldAngle(); // current angle towards target
            var distance = Vector2.Distance(
                _transform.ToMapCoordinates(xform.Coordinates).Position,
                _transform.ToMapCoordinates(tXform.Coordinates).Position
            ); // current distance from target

            float dif = (float) Math.Abs(MathHelper.RadiansToDegrees((float) angle) - MathHelper.RadiansToDegrees((float) _transform.GetWorldRotation(xform)) % 360);
            if (dif > 180)
                dif = 360 - dif;
            dif = MathHelper.DegreesToRadians(dif);

            if (dif >= MathHelper.DegreesToRadians(comp.FieldOfView)) // if target is out of FOV, skip it.
            {
                continue;
            }
            if (distance > comp.DefaultSeekingRange) // if target is out of range, skip it.
            {
                continue;
            }

            if (TryComp<ProjectileComponent>(uid, out var projectile) && TryComp<TransformComponent>(projectile.Shooter, out var shooterTransform) && shooterTransform.GridUid.HasValue) // if target is on same grid as shooter, skip it.
            {
                if (Transform(tUid).GridUid == shooterTransform.GridUid)
                {
                    continue;
                }
            }
            Angle angleOffset = angle - _transform.GetWorldRotation(xform);
            float weight = distance / comp.DefaultSeekingRange - dif; // higher weight the better
            if (comp.TargetEntity == tUid)
                weight += 5f;
            weight += tComp.HeatSignature;
            comp.TargetList.Add(new SeekerTargets() { Target = tUid, Weight = weight }); // add target to list with weight
        }
        comp.TargetList = comp.TargetList.OrderByDescending(t => t.Weight).ToList(); // sort targets by weight
        comp.TargetEntity = comp.TargetList.FirstOrDefault()?.Target; // pick the highest weighted target
    }

    public void PredictiveGuidance(EntityUid uid, HeatSeekingComponent comp, TransformComponent xform, float frameTime) // Predictive Guidance, predicts targets position at impact time.
    {
        if (!comp.TargetEntity.HasValue)
            return;
        if (!TryComp<PhysicsComponent>(comp.TargetEntity.Value, out var targetPhysics)) // if target has no physics, skip it.
        {
            comp.TargetEntity = null;
            return;
        }

        var entXform = Transform(comp.TargetEntity.Value); // get target transform
        var distance = Vector2.Distance(
            _transform.ToMapCoordinates(xform.Coordinates).Position,
            _transform.ToMapCoordinates(entXform.Coordinates).Position
        ); // current distance from target
        var angle = (
            _transform.ToMapCoordinates(entXform.Coordinates).Position -
            _transform.ToMapCoordinates(xform.Coordinates).Position
        ).ToWorldAngle(); // current angle towards target

        float dif = (float) Math.Abs(MathHelper.RadiansToDegrees((float) angle) - MathHelper.RadiansToDegrees((float) _transform.GetWorldRotation(xform)) % 360);
        if (dif > 180)
            dif = 360 - dif;
        dif = MathHelper.DegreesToRadians(dif);

        if (dif >= MathHelper.DegreesToRadians(comp.FieldOfView)) // if target is out of FOV, skip it.
        {
            comp.TargetEntity = null;
            return;
        }
        Vector2 targetVelocity = targetPhysics.LinearVelocity; // get target velocity
        float timeToImpact = distance / comp.Speed; // time it will take for the missile to reach the target
        if (timeToImpact < 0.1) { timeToImpact = 0.1f; } // prevent negative time to impact, that messes up guidance
        Vector2 predictedPosition = _transform.ToMapCoordinates(entXform.Coordinates).Position + targetVelocity * timeToImpact; // predict target position at impact time

        Angle targetAngle = (predictedPosition - _transform.ToMapCoordinates(xform.Coordinates).Position).ToWorldAngle(); // the angle the missile will try to face
        _rotate.TryRotateTo(uid, targetAngle, frameTime, comp.WeaponArc, comp.RotationSpeed?.Theta ?? double.MaxValue, xform); // rotate towards target angle
    }

    public void PurePursuit(EntityUid uid, HeatSeekingComponent comp, TransformComponent xform, float frameTime) // Pure Pursuit, points directly at target.
    {
        if (comp.TargetEntity.HasValue)
        {
            var entXform = Transform(comp.TargetEntity.Value); // get target transform

            var angle = (
                _transform.ToMapCoordinates(entXform.Coordinates).Position -
                _transform.ToMapCoordinates(xform.Coordinates).Position
            ).ToWorldAngle(); // current angle towards target

            float dif = (float) Math.Abs(MathHelper.RadiansToDegrees((float) angle) - MathHelper.RadiansToDegrees((float) _transform.GetWorldRotation(xform)) % 360);
            if (dif > 180)
                dif = 360 - dif;
            dif = MathHelper.DegreesToRadians(dif);

            if (dif >= MathHelper.DegreesToRadians(comp.FieldOfView)) // if target is out of FOV, skip it.
            {
                comp.TargetEntity = null;
                return;
            }

            _rotate.TryRotateTo(uid, angle, frameTime, comp.WeaponArc, comp.RotationSpeed?.Theta ?? double.MaxValue, xform); // rotate towards target angle
        }
    }
}
