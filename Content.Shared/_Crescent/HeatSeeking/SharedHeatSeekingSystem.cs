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
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HeatSeekingComponent, TransformComponent>(); // get all heat seeking missiles
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!TryComp<PhysicsComponent>(uid, out var physics))
                continue;
            if (comp.StartDelay >= 0f) comp.StartDelay -= frameTime; 
            else if (comp.Fuel > 0f)
            {
                if (comp.Speed < comp.TopSpeed)
                    comp.Speed = physics.LinearVelocity.Length() + comp.Acceleration * frameTime; 
                _physics.SetLinearVelocity(uid, _transform.GetWorldRotation(xform).ToWorldVec().Normalized() * comp.Speed);
                comp.Fuel -= frameTime;
            }
            if (comp.RefreshTicker <= comp.RefreshRate)
            {
                comp.RefreshTicker += frameTime;
                QuickRefresh(uid, comp, xform);
            }
            else
            {
                RefreshTargetList(uid, comp, xform);
                comp.RefreshTicker = 0f;
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

            if (angle > _transform.GetWorldRotation(xform) + comp.FOV / 2 * Math.PI / 180f
            || angle < _transform.GetWorldRotation(xform) - comp.FOV / 2 * Math.PI / 180f) // if target is out of FOV, skip it.
            {
                continue;
            }
            if (distance > comp.DefaultSeekingRange) // if target is out of range, skip it.
            {
                continue;
            }

            if (TryComp<ProjectileComponent>(uid, out var projectile) && TryComp<TransformComponent>(projectile.Shooter, out var shooterTransform)) // if target is on same grid as shooter, skip it.
            {
                var shooterGridUid = shooterTransform.GridUid;
                if (Transform(tUid).GridUid == shooterGridUid)
                {
                    continue;
                }
            }

            float dif = (float) Math.Abs(MathHelper.RadiansToDegrees((float)angle) - MathHelper.RadiansToDegrees((float) _transform.GetWorldRotation(xform)) % 360);
            if (dif > 180)
                dif = 360 - dif;
            dif = MathHelper.DegreesToRadians(dif);
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

    public void QuickRefresh(EntityUid uid, HeatSeekingComponent component, TransformComponent transform)
    {
        foreach (SeekerTargets target in component.TargetList)
        {
            var tXform = Transform(target.Target);

            var angle = (
                _transform.ToMapCoordinates(tXform.Coordinates).Position -
                _transform.ToMapCoordinates(transform.Coordinates).Position
            ).ToWorldAngle(); // current angle towards target
            var distance = Vector2.Distance(
                _transform.ToMapCoordinates(transform.Coordinates).Position,
                _transform.ToMapCoordinates(tXform.Coordinates).Position
            ); // current distance from target
            if (angle > _transform.GetWorldRotation(transform) + component.FOV / 2 * Math.PI / 180f
            || angle < _transform.GetWorldRotation(transform) - component.FOV / 2 * Math.PI / 180f) // if target is out of FOV, skip it.
            {
                component.TargetList.Remove(target);
            }
            if (distance > component.DefaultSeekingRange) // if target is out of range, skip it.
            {
                component.TargetList.Remove(target);
            }
            float dif = (float) Math.Abs(MathHelper.RadiansToDegrees((float) angle) - MathHelper.RadiansToDegrees((float) _transform.GetWorldRotation(transform)) % 360);
            if (dif > 180)
                dif = 360 - dif;
            dif = MathHelper.DegreesToRadians(dif);
            Angle angleOffset = angle - _transform.GetWorldRotation(transform);
            float weight = distance / component.DefaultSeekingRange - dif; // higher weight the better
            if (component.TargetEntity == target.Target)
                weight += 5f;
            if (TryComp<CanBeHeatTrackedComponent>(target.Target, out var tComp))
                weight += tComp.HeatSignature;
            target.Weight = weight;
        }
        component.TargetList = component.TargetList.OrderByDescending(t => t.Weight).ToList(); // sort targets by weight
        component.TargetEntity = component.TargetList.FirstOrDefault()?.Target; // pick the highest weighted target
    }


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
        var targetVelocity = _transform.ToMapCoordinates(entXform.Coordinates).Position - comp.oldPosition; // get target velocity
        float timeToImpact = distance / (oldDistance - distance); // time it will take for the missile to reach the target
        if (timeToImpact < 0.1) { timeToImpact = 0.1f; } // prevent negative time to impact, that messes up guidance
        var predictedPosition = _transform.ToMapCoordinates(entXform.Coordinates).Position + (targetVelocity * timeToImpact); // predict target position at impact time

        Angle targetAngle = (predictedPosition - _transform.ToMapCoordinates(xform.Coordinates).Position).ToWorldAngle(); // the angle the missile will try to face
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
