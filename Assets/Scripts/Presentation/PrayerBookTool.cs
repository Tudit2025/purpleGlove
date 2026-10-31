using UnityEngine;

namespace PurpleGlove
{
    /// <summary>The shared reference book opens at the cipher spread and returns to its original desk pose.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SceneObject))]
    public sealed class PrayerBookTool : SceneObjectAction
    {
        public CipherWorkbench workbench;
        public ActArchiveController archive;
        public SceneObject note;
        public GameObject spread;
        public Renderer[] closedSurfaces;
        public bool sevenPetal;
        public override bool IsBusy => workbench && workbench.BookTool == this;
        SceneObject book;
        Vector3 bookPosition, bookScale, notePosition, noteScale;
        Quaternion bookRotation, noteRotation;
        bool[] surfaceStates;
        bool bookEnabled, noteEnabled, bookColliderEnabled, noteColliderEnabled;

        public override void Activate() => TryOpen();
        public bool TryOpen()
        {
            if (!isActiveAndEnabled || !archive || archive.DeskAct != 2 || !archive.IsDocumentAvailable(2, note)
                || !workbench || !spread || IsBusy) return false;
            return workbench.OpenBook(this);
        }

        internal void PlaceOnWorkbench()
        {
            book = GetComponent<SceneObject>(); book.RestoreImmediately(); note.RestoreImmediately();
            bookPosition = transform.localPosition; bookRotation = transform.localRotation; bookScale = transform.localScale;
            notePosition = note.transform.localPosition; noteRotation = note.transform.localRotation; noteScale = note.transform.localScale;
            bookEnabled = book.enabled; noteEnabled = note.enabled; book.enabled = note.enabled = false;
            bookColliderEnabled = book.GetComponent<Collider>().enabled; noteColliderEnabled = note.GetComponent<Collider>().enabled;
            book.GetComponent<Collider>().enabled = note.GetComponent<Collider>().enabled = false;
            surfaceStates = new bool[closedSurfaces.Length];
            for (int i = 0; i < closedSurfaces.Length; i++)
            { surfaceStates[i] = closedSurfaces[i].enabled; closedSurfaces[i].enabled = false; }
            transform.SetPositionAndRotation(workbench.mat.TransformPoint(new Vector3(0, 0.02f, -0.09f)), workbench.mat.rotation);
            transform.localScale = Vector3.one;
            spread.SetActive(true);
            note.transform.SetPositionAndRotation(workbench.mat.TransformPoint(new Vector3(-0.57f, 0.03f, -0.04f)), workbench.mat.rotation);
            note.transform.localScale = Vector3.one;
        }

        internal void RestoreBook()
        {
            if (!book) return;
            spread.SetActive(false);
            for (int i = 0; i < closedSurfaces.Length; i++) if (closedSurfaces[i]) closedSurfaces[i].enabled = surfaceStates[i];
            transform.localPosition = bookPosition; transform.localRotation = bookRotation; transform.localScale = bookScale;
            book.enabled = bookEnabled; book.GetComponent<Collider>().enabled = bookColliderEnabled;
            if (note)
            {
                note.transform.localPosition = notePosition; note.transform.localRotation = noteRotation; note.transform.localScale = noteScale;
                note.enabled = noteEnabled; note.GetComponent<Collider>().enabled = noteColliderEnabled;
            }
            book = null;
        }
        public override void Cancel() { if (IsBusy) workbench.CloseBook(); }
        void OnDisable() => Cancel();
    }
}
