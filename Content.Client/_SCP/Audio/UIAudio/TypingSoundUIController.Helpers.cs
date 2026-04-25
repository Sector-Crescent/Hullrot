using Content.Client.UserInterface.Systems.Chat.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._Scp.Audio.UIAudio;

public sealed partial class TypingSoundUIController
{
    private static PendingKeyAction MapPendingAction(BoundKeyFunction function)
    {
        if (function == EngineKeyFunctions.TextPaste)
            return PendingKeyAction.Paste;

        if (function == EngineKeyFunctions.TextCut
            || function == EngineKeyFunctions.TextBackspace
            || function == EngineKeyFunctions.TextDelete
            || function == EngineKeyFunctions.TextWordBackspace
            || function == EngineKeyFunctions.TextWordDelete)
        {
            return PendingKeyAction.Delete;
        }

        return PendingKeyAction.None;
    }

    private static bool IsSubmitFunction(BoundKeyFunction function)
    {
        return function == EngineKeyFunctions.TextSubmit
            || function == EngineKeyFunctions.MultilineTextSubmit;
    }

    private static bool IsGameChatInput(Control control)
    {
        for (var current = control.Parent; current != null; current = current.Parent)
        {
            if (current is ChatInputBox)
                return true;
        }

        return false;
    }

    private static TypingSoundKind? ClassifyChange(string previousText, string newText, PendingKeyAction pendingAction)
    {
        if (previousText == newText)
            return null;

        return pendingAction switch
        {
            PendingKeyAction.Paste => TypingSoundKind.Paste,
            PendingKeyAction.Delete => TypingSoundKind.Delete,
            PendingKeyAction.ChatSubmit => null,
            _ => ClassifyByTextDelta(previousText, newText),
        };
    }

    private static TypingSoundKind? ClassifyByTextDelta(string previousText, string newText)
    {
        var previousRunes = CountRunes(previousText);
        var newRunes = CountRunes(newText);

        if (newRunes > previousRunes)
            return newRunes - previousRunes > 1 ? TypingSoundKind.Paste : TypingSoundKind.Input;

        if (newRunes < previousRunes)
            return TypingSoundKind.Delete;

        return TypingSoundKind.Input;
    }

    private static int CountRunes(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        var count = 0;
        foreach (var _ in text.EnumerateRunes())
        {
            count++;
        }

        return count;
    }
}
