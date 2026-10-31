using System;
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
        public DeskScenePreview view;
        public SceneObjectInteractor interactor;
        [Tooltip("Invoked once after an act has been archived. The argument is the completed act (1-5).")]
        public UnityEvent<int> actCompleted = new UnityEvent<int>();
        public int CurrentAct { get; private set; } = 1;
        public int DeskAct { get; private set; } = 1;
        public bool AllActsCompleted => CurrentAct > 5;
        bool initialized, changing;

        void Awake() => Initialize();

        void Initialize()
        {
            if (initialized) return;
            if (acts == null || acts.Length != 5)
                throw new InvalidOperationException("The archive must have exactly five act shelves.");
            // Validate all references before touching any objects.
            for (int i = 0; i < acts.Length; i++)
            {
                var files = acts[i];
                if (files == null || !files.storageAnchor || !files.bundle)
                    throw new InvalidOperationException("Missing archive shelf or bundle for act " + (i + 1));
                files.poses = new DeskPose[files.documents.Length];
                for (int j = 0; j < files.documents.Length; j++)
                {
                    if (!files.documents[j]) throw new InvalidOperationException("Missing document in act " + (i + 1));
                    var t = files.documents[j].transform;
                    files.poses[j] = new DeskPose { parent = t.parent, position = t.localPosition, rotation = t.localRotation, scale = t.localScale };
                }
            }
            initialized = true;
            for (int i = 0; i < acts.Length; i++)
            {
                acts[i].available = i == 0;
                acts[i].bundle.archive = this;
                acts[i].bundle.act = i + 1;
                acts[i].bundle.gameObject.SetActive(false);
                foreach (var document in acts[i].documents) document.gameObject.SetActive(i == 0);
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
                if (!AllActsCompleted)
                {
                    acts[CurrentAct - 1].available = true;
                    PlaceOnDesk(CurrentAct);
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

        /// <summary>Register a document delivered by the story at its intended desk pose.</summary>
        public bool RegisterDocument(int act, SceneObject document)
        {
            Initialize();
            if (changing || !document || act < 1 || act > 5 || !acts[act - 1].available) return false;
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
            if (DeskAct != act)
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
                document.Relocate(files.storageAnchor, new Vector3(0, 0.015f * i, 0), Quaternion.identity, Vector3.one * 0.65f);
                document.gameObject.SetActive(false);
            }
            files.bundle.gameObject.SetActive(true);
        }

        void PlaceOnDesk(int act)
        {
            var files = acts[act - 1];
            files.bundle.gameObject.SetActive(false);
            for (int i = 0; i < files.documents.Length; i++)
            {
                var document = files.documents[i];
                if (!document) continue;
                var pose = files.poses[i];
                document.Relocate(pose.parent, pose.position, pose.rotation, pose.scale);
                document.gameObject.SetActive(true);
            }
            DeskAct = act;
        }

        void RefreshLabels()
        {
            for (int i = 0; i < acts.Length; i++)
            {
                var files = acts[i];
                if (files.statusLabel)
                    files.statusLabel.text = !files.available ? "未开始" : DeskAct == i + 1 ? "桌面" : files.completed ? "已归档" : "暂存";
            }
        }
    }
}
