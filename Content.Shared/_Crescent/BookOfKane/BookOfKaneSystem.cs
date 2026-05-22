using Content.Shared.Chat;
using Content.Shared.Interaction.Events;
using Content.Shared._Crescent.BookOfKane.Components;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;


namespace Content.Shared._Crescent.BookOfKane;


public sealed class BookOfKaneSystem : EntitySystem
{
    [Dependency] private readonly SharedChatSystem _chat = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BookOfKaneComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<BookOfKaneComponent> ent, ref UseInHandEvent args)
    {
        var datasetId = ent.Comp.BookOfKaneDataset;

        if (datasetId == null)
            return;

        if (!_proto.TryIndex(datasetId.Value, out var dataset))
            return;

        if (dataset.Values.Count == 0)
            return;

        var message = _random.Pick(dataset.Values);

        _chat.TrySendInGameICMessage(
            args.User,
            Loc.GetString(message),
            InGameICChatType.Speak,
            hideChat: false,
            hideLog: false);

        args.Handled = true;
    }
}
