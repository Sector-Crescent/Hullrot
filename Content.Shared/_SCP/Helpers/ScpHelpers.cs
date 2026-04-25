using Content.Shared._Scp.Proximity;
using Content.Shared._Scp.Watching;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Scp.Helpers;

public sealed class ScpHelpers : EntitySystem
{
    [Dependency] private readonly EyeWatchingSystem _watching = default!;

    /// <summary>
    /// Получает суммарное количество реагента в зоне видимости сущности.
    /// Возвращает количество реагентов.
    /// </summary>
    public FixedPoint2 GetAroundSolutionVolume(EntityUid uid,
        ProtoId<ReagentPrototype> reagent,
        List<EntityUid> puddleList,
        LineOfSightBlockerLevel lineOfSight = LineOfSightBlockerLevel.Transparent,
        LookupFlags flags = LookupFlags.StaticSundries | LookupFlags.Sensors | LookupFlags.Approximate)
    {
        using var puddles = ListPoolEntity<PuddleComponent>.Rent();
        if (!_watching.TryGetAllEntitiesVisibleTo(uid, puddles.Value, lineOfSight, flags))
            return FixedPoint2.Zero;

        FixedPoint2 total = 0;
        foreach (var puddle in puddles.Value)
        {
            if (!puddle.Comp.Solution.HasValue)
                continue;

            var solution = puddle.Comp.Solution.Value.Comp.Solution;
            foreach (var (reagentId, quantity) in solution.Contents)
            {
                if (reagentId.Prototype != reagent)
                    continue;

                puddleList.Add(puddle);
                total += quantity;
            }
        }

        return total;
    }

    /// <summary>
    /// Получает суммарное количество реагента в зоне видимости сущности.
    /// Возвращает количество реагентов.
    /// </summary>
    public FixedPoint2 GetAroundSolutionVolume(EntityUid uid,
        ProtoId<ReagentPrototype> reagent,
        LineOfSightBlockerLevel lineOfSight = LineOfSightBlockerLevel.Transparent,
        LookupFlags flags = LookupFlags.StaticSundries | LookupFlags.Sensors | LookupFlags.Approximate)
    {
        using var puddles = ListPoolEntity<PuddleComponent>.Rent();
        if (!_watching.TryGetAllEntitiesVisibleTo(uid, puddles.Value, lineOfSight, flags))
            return FixedPoint2.Zero;

        FixedPoint2 total = 0;
        foreach (var puddle in puddles.Value)
        {
            if (!puddle.Comp.Solution.HasValue)
                continue;

            var solution = puddle.Comp.Solution.Value.Comp.Solution;

            foreach (var (reagentId, quantity) in solution.Contents)
            {
                if (reagentId.Prototype != reagent)
                    continue;

                total += quantity;
            }
        }

        return total;
    }

    public bool IsAroundSolutionVolumeGreaterThan(EntityUid uid,
        ProtoId<ReagentPrototype> reagent,
        FixedPoint2 required,
        LineOfSightBlockerLevel lineOfSight = LineOfSightBlockerLevel.Transparent,
        LookupFlags flags = LookupFlags.StaticSundries | LookupFlags.Sensors | LookupFlags.Approximate)
    {
        using var puddles = ListPoolEntity<PuddleComponent>.Rent();
        if (!_watching.TryGetAllEntitiesVisibleTo(uid, puddles.Value, lineOfSight, flags))
            return false;

        FixedPoint2 total = 0;
        foreach (var puddle in puddles.Value)
        {
            if (!puddle.Comp.Solution.HasValue)
                continue;

            var solution = puddle.Comp.Solution.Value.Comp.Solution;

            foreach (var (reagentId, quantity) in solution.Contents)
            {
                if (reagentId.Prototype != reagent)
                    continue;

                total += quantity;

                if (total >= required)
                    return true;

            }
        }

        return false;
    }

    /// <summary>
    /// Вычисляет плавно сглаженную громкость в dB по расстоянию с логарифмическим падением.
    /// </summary>
    /// <param name="distance">Текущее расстояние от источника</param>
    /// <param name="minDistance">Расстояние, на котором громкость = nearDb</param>
    /// <param name="maxDistance">Расстояние, на котором громкость = farDb</param>
    /// <param name="nearDb">Громкость у источника в dB</param>
    /// <param name="farDb">Громкость на пределе слышимости в dB</param>
    /// <param name="previousDb">Предыдущая громкость в dB (для сглаживания)</param>
    /// <param name="deltaTime">Время между кадрами</param>
    /// <param name="smoothing">Скорость сглаживания (чем больше, тем быстрее подстраивается)</param>
    /// <param name="curveA">Форма логарифмической кривой (0 = линейно, >0 = логарифмически)</param>
    /// <returns>Плавно сглаженная громкость в dB</returns>
    public static float SmoothVolumeDb(
        float distance,
        float minDistance,
        float maxDistance,
        float nearDb,
        float farDb,
        ref float previousDb,
        float deltaTime,
        float smoothing = 5f,
        float curveA = 2f)
    {
        // --- Логарифмическая интерполяция ---
        float t;
        if (distance <= minDistance)
            t = 0f;
        else if (distance >= maxDistance)
            t = 1f;
        else
            t = (distance - minDistance) / (maxDistance - minDistance);

        var shaped = MathF.Abs(curveA) < 1e-6f
            ? t
            : MathF.Log(1f + curveA * t) / MathF.Log(1f + curveA);

        var targetDb = MathHelper.Lerp(nearDb, farDb, shaped);

        // --- Плавное сглаживание ---
        var lerpFactor = MathHelper.Clamp(smoothing * deltaTime, 0f, 1f);
        previousDb = MathHelper.Lerp(previousDb, targetDb, lerpFactor);

        return previousDb;
    }
}
