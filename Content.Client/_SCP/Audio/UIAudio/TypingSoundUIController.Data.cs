using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Scp.Audio.UIAudio;

public sealed partial class TypingSoundUIController
{
    private sealed class ControlTracking
    {
        public Action<Control> ChildAdded { get; init; } = default!;
        public Action<Control> ChildRemoved { get; init; } = default!;
    }

    private sealed class LineEditTracking
    {
        public string PreviousText = string.Empty;
        public PendingKeyAction PendingAction;
        public long Revision;
        public long ProcessedRevision;
        public Action<GUIBoundKeyEventArgs> KeyBindDown { get; set; } = default!;
        public Action<LineEdit.LineEditEventArgs> TextChanged { get; set; } = default!;
        public Action<GUITextEnteredEventArgs> TextTyped { get; set; } = default!;
        public Action<LineEdit.LineEditTextRemovedEventArgs> TextRemoved { get; set; } = default!;
        public Action<LineEdit.LineEditEventArgs> TextEntered { get; set; } = default!;
    }

    private sealed class TextEditTracking
    {
        public string PreviousText = string.Empty;
        public PendingKeyAction PendingAction;
        public Action<GUIBoundKeyEventArgs> KeyBindDown { get; set; } = default!;
        public Action<TextEdit.TextEditEventArgs> TextChanged { get; set; } = default!;
    }

    private enum PendingKeyAction : byte
    {
        None,
        Paste,
        Delete,
        ChatSubmit,
    }

    private enum TypingSoundKind : byte
    {
        Input,
        Paste,
        Submit,
        ChatSubmit,
        Delete,
    }
}
