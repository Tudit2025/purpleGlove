using UnityEngine;

namespace PurpleGlove
{
    public sealed partial class CipherWorkbench
    {
        public PrayerBookTool BookTool { get; private set; }

        public bool OpenBook(PrayerBookTool book)
        {
            if (!book || Interrupted || IsManipulating || !view || (gate && gate.BlocksWorldInput)) return false;
            if (interactor) interactor.CancelInteraction();
            CloseBook(); CloseToolbox(); EndWriting();
            pendingDesktop = pendingFocus = false;
            if (!view.SetView(DeskScenePreview.View.DecodeFocus)) return false;
            // Book reading shares the existing plaintext keyboard, with no substitution mapping.
            requestedTool = Tool.None;
            if (ActiveTool != Tool.None) { StartToolTransfer((int)ActiveTool, true); CancelManipulation(); }
            BookTool = book; book.PlaceOnWorkbench();
            view.FocusBookSpread();
            decodedStrokes.Clear(); RefreshKeyboardMapping(); Play(takeSound);
            if (interactor) interactor.TrackAction(book);
            return true;
        }

        public void CloseBook()
        {
            if (!BookTool) return;
            var book = BookTool; BookTool = null;
            book.RestoreBook(); SuspendDecodeKeyboard(); decodedStrokes.Clear();
            RefreshKeyboardMapping(); Play(returnSound);
        }
    }
}
