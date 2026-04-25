using System.Threading;
using Content.Shared.GameTicking;
using Robust.Shared.Serialization;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Shared._Scp.Shaders.Highlighting;

/// <summary>
/// Система-помощник для подсвечивания сущностей через шейдер подсвечивания
/// </summary>
public abstract class SharedHighlightSystem : EntitySystem
{
    /// <summary>
    /// Примерное время одного подсвечивания.
    /// </summary>
    public static readonly TimeSpan OneHighlightTime = TimeSpan.FromSeconds(1.6f);

    protected CancellationTokenSource Token = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => RecreateToken());
    }

    /// <summary>
    /// Подсвечивает сущность указанное количество раз.
    /// Чтобы подсвечивать бесконечно передать -1.
    /// </summary>
    /// <param name="target">Сущность, которая будет подсвечена</param>
    /// <param name="recipient">Сущность, которая увидит подсвечивание. Если null, то его увидят все</param>
    /// <param name="highlightTimes">Количество раз подсвечивания</param>
    public void Highlight(EntityUid target, EntityUid? recipient = null, int highlightTimes = 3)
    {
        var comp = EnsureComp<HighlightedComponent>(target);

        if (recipient.HasValue)
        {
            comp.Recipient = recipient;
            Dirty(target, comp);
        }

        var ev = new HighLightStartEvent();
        RaiseLocalEvent(target, ev);

        if (highlightTimes == -1)
            return;

        var time = OneHighlightTime * highlightTimes;

        Timer.Spawn(time,
            () =>
            {
                if (!Exists(target))
                    return;

                var endEvent = new HighLightEndEvent();
                RaiseLocalEvent(target, endEvent);

                RemCompDeferred<HighlightedComponent>(target);
            },
            Token.Token);
    }

    /// <summary>
    /// Подсвечивает все сущности из списка
    /// </summary>
    public void HighLightAll(IEnumerable<EntityUid> list, EntityUid? recipient = null)
    {
        foreach (var uid in list)
        {
            Highlight(uid, recipient);
        }
    }

    private void RecreateToken()
    {
        Token.Cancel();
        Token = new();
    }
}

[Serializable, NetSerializable]
public sealed class HighLightStartEvent(NetEntity? entity = null) : EntityEventArgs
{
    public NetEntity? Entity = entity;
}

[Serializable, NetSerializable]
public sealed class HighLightEndEvent(NetEntity? entity = null) : EntityEventArgs
{
    public NetEntity? Entity = entity;
}
