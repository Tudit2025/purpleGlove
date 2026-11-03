using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PurpleGlove
{
    /// <summary>Owns the five act dossiers and swaps their original document instances with the desk.</summary>
    [DisallowMultipleComponent]
    public sealed class ActArchiveController : MonoBehaviour
    {
        [Serializable]
        public sealed class ActFiles
        {
            public string title;
            public SceneObject[] documents = Array.Empty<SceneObject>();
            public SceneObject[] deferredDocuments = Array.Empty<SceneObject>();
            [NonSerialized] public bool supplementReceived;
            public Transform storageAnchor;
            public ActArchiveBundle bundle;
            public TextMesh statusLabel;
            [NonSerialized] public bool available;
            [NonSerialized] public bool completed;
            [NonSerialized] internal DeskPose[] poses;
        }

        internal struct DeskPose
        {
            public Transform parent;
            public Vector3 position, scale;
            public Quaternion rotation;
        }

        public ActFiles[] acts = new ActFiles[5];
        [Tooltip("Reference books and the master's notebook never enter an act dossier.")]
        public SceneObject[] sharedDocuments = Array.Empty<SceneObject>();
        [Tooltip("Preview all five document sets. Disable when story delivery controls availability.")]
        public bool previewAllActs = true;
        [Tooltip("Keep every act locked until its sealed packet has been collected, including act one.")]
        public bool requireDelivery;
        public DeskScenePreview view;
        public SceneObjectInteractor interactor;
        [Tooltip("Invoked once after an act has been archived. The argument is the completed act (1-5).")]
        public UnityEvent<int> actCompleted = new UnityEvent<int>();
        [Tooltip("Invoked when a newly received act's packet has landed and its documents are displayed.")]
        public UnityEvent<int> actReceived = new UnityEvent<int>();
        public int LatestReceivedAct { get; private set; }
        public int CurrentAct { get; private set; } = 1;
        public int DeskAct { get; private set; }
        public bool AllActsCompleted => CurrentAct > 5;
        bool initialized, changing;
        readonly HashSet<SceneObject> withdrawn = new HashSet<SceneObject>();

        void Awake() => Initialize();

        void Initialize()
        {
            if (initialized) return;
            if (acts == null || acts.Length != 5)
                throw new InvalidOperationException("The archive must have exactly five act shelves.");
            // Validate all references before touching any objects.
            var registered = new HashSet<SceneObject>(sharedDocuments);
            for (int i = 0; i < acts.Length; i++)
            {
                var files = acts[i];
                if (files == null || !files.storageAnchor || !files.bundle)
                    throw new InvalidOperationException("Missing archive shelf or bundle for act " + (i + 1));
                files.poses = new DeskPose[files.documents.Length];
                for (int j = 0; j < files.documents.Length; j++)
                {
                    if (!files.documents[j]) throw new InvalidOperationException("Missing document in act " + (i + 1));
                    if (!registered.Add(files.documents[j]))
                        throw new InvalidOperationException("A document is shared or assigned to more than one act: " + files.documents[j].name);
                    var t = files.documents[j].transform;
                    files.poses[j] = new DeskPose { parent = t.parent, position = t.localPosition, rotation = t.localRotation, scale = t.localScale };
                }
            }
            initialized = true;
            for (int i = 0; i < acts.Length; i++)
            {
                acts[i].available = previewAllActs || (!requireDelivery && i == 0);
                acts[i].bundle.archive = this;
                acts[i].bundle.act = i + 1;
                Store(i + 1);
            }
            RefreshLabels();
        }

        /// <summary>Call this from the story's end-of-act event, including while reviewing an older act.</summary>
        public bool CompleteAct(int act)
        {
            Initialize();
            if (changing || act < 1 || act > 5 || act != CurrentAct || acts[act - 1].completed) return false;
            changing = true;
            try
            {
                if (interactor) interactor.CancelInteraction();
                if (DeskAct > 0) Store(DeskAct);
                acts[act - 1].completed = true;
                CurrentAct++;
                DeskAct = 0;
                if (!AllActsCompleted && !requireDelivery)
                {
                    acts[CurrentAct - 1].available = true;
                    acts[CurrentAct - 1].bundle.gameObject.SetActive(true);
                }
                RefreshLabels();
            }
            finally { changing = false; }
            actCompleted.Invoke(act);
            return true;
        }

        // A void entry point can also be wired directly from an Inspector UnityEvent.
        public void EndAct(int act) => CompleteAct(act);
        public void CompleteCurrentAct() => CompleteAct(CurrentAct);

        public bool ShowActOnDesk(int act)
        {
            Initialize();
            if (changing || act < 1 || act > 5 || !acts[act - 1].available) return false;
            changing = true;
            try
            {
                if (interactor) interactor.CancelInteraction();
                if (DeskAct != act)
                {
                    if (DeskAct > 0) Store(DeskAct);
                    PlaceOnDesk(act);
                }
                RefreshLabels();
                if (view) view.SetView(DeskScenePreview.View.Desktop);
            }
            finally { changing = false; }
            return true;
        }

        public bool IsAvailable(int act) => initialized && act >= 1 && act <= 5 && acts[act - 1].available;
        public bool IsCompleted(int act) => initialized && act >= 1 && act <= 5 && acts[act - 1].completed;

        public bool HasPendingSupplement(int act) => IsAvailable(act) && !previewAllActs
            && acts[act - 1].deferredDocuments.Length > 0 && !acts[act - 1].supplementReceived;

        public bool IsDocumentAvailable(int act, SceneObject document) => IsAvailable(act)
            && document && !withdrawn.Contains(document)
            && Array.IndexOf(acts[act - 1].documents, document) >= 0
            && (!HasPendingSupplement(act) || Array.IndexOf(acts[act - 1].deferredDocuments, document) < 0);

        // Burned or dispatched originals cannot reappear when an older dossier is recalled.
        public bool WithdrawDocument(SceneObject document)
        {
            if (!document || !withdrawn.Add(document)) return false;
            document.gameObject.SetActive(false);
            return true;
        }

        public bool ReceiveSupplement(int act)
        {
            if (changing || !HasPendingSupplement(act)) return false;
            if (interactor) interactor.CancelInteraction();
            acts[act - 1].supplementReceived = true;
            if (DeskAct == act) PlaceOnDesk(act);
            return ShowActOnDesk(act);
        }

        /// <summary>Unlock only once the delivery has landed, then use the original authored desk layout.</summary>
        public bool ReceiveAct(int act)
        {
            Initialize();
            if (changing || act < 1 || act > acts.Length || acts[act - 1].available) return false;
            acts[act - 1].available = true;
            if (!ShowActOnDesk(act)) return false;
            LatestReceivedAct = Mathf.Max(LatestReceivedAct, act);
            actReceived.Invoke(act);
            return true;
        }

        public bool StoreDeskAct()
        {
            Initialize();
            if (changing || DeskAct == 0) return false;
            changing = true;
            try
            {
                if (interactor) interactor.CancelInteraction();
                Store(DeskAct);
                DeskAct = 0;
                RefreshLabels();
            }
            finally { changing = false; }
            return true;
        }

        /// <summary>Register a document delivered by the story at its intended desk pose.</summary>
        public bool RegisterDocument(int act, SceneObject document)
        {
            Initialize();
            if (changing || !document || act < 1 || act > 5 || !acts[act - 1].available
                || Array.IndexOf(sharedDocuments, document) >= 0) return false;
            foreach (var entry in acts)
                if (Array.IndexOf(entry.documents, document) >= 0) return false;
            if (interactor) interactor.CancelInteraction();
            var files = acts[act - 1];
            var t = document.transform;
            int index = files.documents.Length;
            Array.Resize(ref files.documents, index + 1);
            Array.Resize(ref files.poses, index + 1);
            files.documents[index] = document;
            files.poses[index] = new DeskPose { parent = t.parent, position = t.localPosition, rotation = t.localRotation, scale = t.localScale };
            var workingForm = document.GetComponent<DeskOperationalDocument>();
            if (workingForm && workingForm.UsesRack) workingForm.Stow(true);
            else if (DeskAct != act)
            {
                document.Relocate(files.storageAnchor, new Vector3(0, 0.015f * index, 0), Quaternion.identity, Vector3.one * 0.65f);
                document.gameObject.SetActive(false);
            }
            else document.gameObject.SetActive(true);
            return true;
        }

        void Store(int act)
        {
            var files = acts[act - 1];
            for (int i = 0; i < files.documents.Length; i++)
            {
                var document = files.documents[i];
                if (!document) continue;
                if (withdrawn.Contains(document)) { document.gameObject.SetActive(false); continue; }
                var workingForm = document.GetComponent<DeskOperationalDocument>();
                if (workingForm && workingForm.UsesRack) { workingForm.Stow(IsDocumentAvailable(act, document)); continue; }
                document.Relocate(files.storageAnchor, new Vector3(0, 0.015f * i, 0), Quaternion.identity, Vector3.one * 0.65f);
                document.gameObject.SetActive(false);
            }
            files.bundle.gameObject.SetActive(files.available);
        }

        void PlaceOnDesk(int act)
        {
            var files = acts[act - 1];
            files.bundle.gameObject.SetActive(false);
            for (int i = 0; i < files.documents.Length; i++)
            {
                var document = files.documents[i];
                if (!document) continue;
                if (withdrawn.Contains(document)) { document.gameObject.SetActive(false); continue; }
                var workingForm = document.GetComponent<DeskOperationalDocument>();
                if (workingForm && workingForm.UsesRack) { workingForm.Stow(IsDocumentAvailable(act, document)); continue; }
                var pose = files.poses[i];
                document.Relocate(pose.parent, pose.position, pose.rotation, pose.scale);
                document.gameObject.SetActive(IsDocumentAvailable(act, document));
            }
            DeskAct = act;
        }

        void RefreshLabels()
        {
            for (int i = 0; i < acts.Length; i++)
            {
                var files = acts[i];
                if (files.statusLabel)
                    Localization.SetText(files.statusLabel, !files.available ? "未启封" : DeskAct == i + 1 ? "案头" : files.completed ? "已归档" : "暂存");
            }
        }
    }
}
