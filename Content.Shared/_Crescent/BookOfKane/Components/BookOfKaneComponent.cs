using Content.Shared.Dataset;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Crescent.BookOfKane.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class BookOfKaneComponent : Component
{
    [DataField]
    public ProtoId<LocalizedDatasetPrototype>? bookOfKaneDataset = "BookOfKaneSpeech";
}
