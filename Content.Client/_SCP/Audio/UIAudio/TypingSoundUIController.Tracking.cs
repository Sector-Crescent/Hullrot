using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Scp.Audio.UIAudio;

public sealed partial class TypingSoundUIController
{
    private void TrackSubtree(Control control)
    {
        if (control.Disposed || _trackedControls.ContainsKey(control))
            return;

        var tracking = new ControlTracking
        {
            ChildAdded = TrackSubtree,
            ChildRemoved = UntrackSubtree,
        };

        control.OnChildAdded += tracking.ChildAdded;
        control.OnChildRemoved += tracking.ChildRemoved;
        _trackedControls[control] = tracking;

        switch (control)
        {
            case LineEdit lineEdit:
                TrackLineEdit(lineEdit);
                break;
            case TextEdit textEdit:
                TrackTextEdit(textEdit);
                break;
        }

        foreach (var child in control.Children)
        {
            TrackSubtree(child);
        }
    }

    private void UntrackSubtree(Control control)
    {
        foreach (var child in control.Children)
        {
            UntrackSubtree(child);
        }

        if (control is LineEdit lineEdit && _trackedLineEdits.ContainsKey(lineEdit))
            UntrackLineEdit(lineEdit);

        if (control is TextEdit textEdit && _trackedTextEdits.ContainsKey(textEdit))
            UntrackTextEdit(textEdit);

        if (!_trackedControls.Remove(control, out var tracking))
            return;

        control.OnChildAdded -= tracking.ChildAdded;
        control.OnChildRemoved -= tracking.ChildRemoved;
    }

    private void TrackLineEdit(LineEdit control)
    {
        if (_trackedLineEdits.ContainsKey(control))
            return;

        var tracking = new LineEditTracking
        {
            PreviousText = control.Text,
        };

        tracking.KeyBindDown = args => OnLineEditKeyBindDown(control, args);
        tracking.TextChanged = args => OnLineEditTextChanged(control, args);
        tracking.TextTyped = args => OnLineEditTextTyped(control, args);
        tracking.TextRemoved = args => OnLineEditTextRemoved(control, args);
        tracking.TextEntered = args => OnLineEditTextEntered(control, args);

        control.OnKeyBindDown += tracking.KeyBindDown;
        control.OnTextChanged += tracking.TextChanged;
        control.OnTextTyped += tracking.TextTyped;
        control.OnTextRemoved += tracking.TextRemoved;
        control.OnTextEntered += tracking.TextEntered;

        _trackedLineEdits[control] = tracking;
    }

    private void UntrackLineEdit(LineEdit control)
    {
        if (!_trackedLineEdits.Remove(control, out var tracking))
            return;

        control.OnKeyBindDown -= tracking.KeyBindDown;
        control.OnTextChanged -= tracking.TextChanged;
        control.OnTextTyped -= tracking.TextTyped;
        control.OnTextRemoved -= tracking.TextRemoved;
        control.OnTextEntered -= tracking.TextEntered;
    }

    private void TrackTextEdit(TextEdit control)
    {
        if (_trackedTextEdits.ContainsKey(control))
            return;

        var tracking = new TextEditTracking
        {
            PreviousText = Rope.Collapse(control.TextRope),
        };

        tracking.KeyBindDown = args => OnTextEditKeyBindDown(control, args);
        tracking.TextChanged = args => OnTextEditTextChanged(control, args);

        control.OnKeyBindDown += tracking.KeyBindDown;
        control.OnTextChanged += tracking.TextChanged;

        _trackedTextEdits[control] = tracking;
    }

    private void UntrackTextEdit(TextEdit control)
    {
        if (!_trackedTextEdits.Remove(control, out var tracking))
            return;

        control.OnKeyBindDown -= tracking.KeyBindDown;
        control.OnTextChanged -= tracking.TextChanged;
    }

    private void OnLineEditKeyBindDown(LineEdit control, GUIBoundKeyEventArgs args)
    {
        if (!_trackedLineEdits.TryGetValue(control, out var tracking))
            return;

        var action = MapPendingAction(args.Function);
        if (action != PendingKeyAction.None)
            tracking.PendingAction = action;
    }

    private void OnLineEditTextChanged(LineEdit control, LineEdit.LineEditEventArgs args)
    {
        if (!_trackedLineEdits.TryGetValue(control, out var tracking))
            return;

        if (tracking.PreviousText == args.Text)
            return;

        var revision = ++tracking.Revision;
        var previousText = tracking.PreviousText;
        var newText = args.Text;
        var pendingAction = tracking.PendingAction;

        UIManager.DeferAction(() => FinalizeLineEditChange(control, revision, previousText, newText, pendingAction));
    }

    private void OnLineEditTextTyped(LineEdit control, GUITextEnteredEventArgs args)
    {
        if (!_trackedLineEdits.TryGetValue(control, out var tracking))
            return;

        tracking.ProcessedRevision = tracking.Revision;
        tracking.PreviousText = control.Text;
        var pendingAction = tracking.PendingAction;
        var kind = pendingAction == PendingKeyAction.Paste || CountRunes(args.Text) > 1
            ? TypingSoundKind.Paste
            : TypingSoundKind.Input;
        tracking.PendingAction = PendingKeyAction.None;
        Play(control, kind);
    }

    private void OnLineEditTextRemoved(LineEdit control, LineEdit.LineEditTextRemovedEventArgs args)
    {
        if (!_trackedLineEdits.TryGetValue(control, out var tracking))
            return;

        tracking.ProcessedRevision = tracking.Revision;
        tracking.PreviousText = args.NewText;
        tracking.PendingAction = PendingKeyAction.None;

        Play(control, TypingSoundKind.Delete);
    }

    private void OnLineEditTextEntered(LineEdit control, LineEdit.LineEditEventArgs args)
    {
        _ = args;

        if (!_trackedLineEdits.TryGetValue(control, out var tracking))
            return;

        if (IsGameChatInput(control))
        {
            tracking.PendingAction = PendingKeyAction.ChatSubmit;
            return;
        }

        tracking.PendingAction = PendingKeyAction.None;
        Play(control, TypingSoundKind.Submit);
    }

    private void FinalizeLineEditChange(
        LineEdit control,
        long revision,
        string previousText,
        string newText,
        PendingKeyAction pendingAction)
    {
        if (!_trackedLineEdits.TryGetValue(control, out var tracking))
            return;

        if (tracking.ProcessedRevision >= revision)
            return;

        if (tracking.PreviousText != previousText)
            return;

        tracking.ProcessedRevision = revision;
        tracking.PreviousText = newText;
        tracking.PendingAction = PendingKeyAction.None;

        var kind = ClassifyChange(previousText, newText, pendingAction);
        if (kind == null)
            return;

        Play(control, kind.Value);
    }

    private void OnTextEditKeyBindDown(TextEdit control, GUIBoundKeyEventArgs args)
    {
        if (!_trackedTextEdits.TryGetValue(control, out var tracking))
            return;

        if (IsSubmitFunction(args.Function))
        {
            tracking.PendingAction = PendingKeyAction.None;
            Play(control, TypingSoundKind.Submit);
            return;
        }

        var action = MapPendingAction(args.Function);
        if (action != PendingKeyAction.None)
            tracking.PendingAction = action;
    }

    private void OnTextEditTextChanged(TextEdit control, TextEdit.TextEditEventArgs args)
    {
        if (!_trackedTextEdits.TryGetValue(control, out var tracking))
            return;

        var newText = Rope.Collapse(args.TextRope);
        var previousText = tracking.PreviousText;

        tracking.PreviousText = newText;

        if (previousText == newText)
        {
            tracking.PendingAction = PendingKeyAction.None;
            return;
        }

        var kind = ClassifyChange(previousText, newText, tracking.PendingAction);
        tracking.PendingAction = PendingKeyAction.None;

        if (kind == null)
            return;

        Play(control, kind.Value);
    }
}
