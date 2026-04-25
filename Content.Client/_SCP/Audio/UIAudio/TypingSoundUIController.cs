using Robust.Client.State;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio.Sources;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Client._Scp.Audio.UIAudio;

public sealed partial class TypingSoundUIController : UIController, IOnStateChanged<State>
{
    [Dependency] private readonly IAudioManager _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IResourceCache _resource = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private const float BaseGain = 0.08f;

    private readonly Dictionary<Control, ControlTracking> _trackedControls = new();
    private readonly Dictionary<LineEdit, LineEditTracking> _trackedLineEdits = new();
    private readonly Dictionary<TextEdit, TextEditTracking> _trackedTextEdits = new();
    private readonly Dictionary<string, IAudioSource> _sourceCache = new();
    private readonly HashSet<string> _invalidSoundWarnings = [];

    private void OnPostDrawRoot(PostDrawUIRootEventArgs args)
    {
        TrackSubtree(args.Root);
    }
}
