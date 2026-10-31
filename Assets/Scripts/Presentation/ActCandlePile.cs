using UnityEngine;

namespace PurpleGlove
{
    /// <summary>Unlit desk reserves track story entry, independently of the dossier on display.</summary>
    [DisallowMultipleComponent]
    public sealed class ActCandlePile : MonoBehaviour
    {
        [Tooltip("Four candles in retention order: the top candle is last and is used first.")]
        public GameObject[] candles = new GameObject[4];
        public ActArchiveController archive;
        public PrologueSequence story;
        public int EnteredAct { get; private set; } = 1;
        public int RemainingCount { get; private set; } = 4;

        void OnEnable()
        {
            if (archive) archive.actReceived.AddListener(EnterAct);
            if (story)
            {
                story.actPrepared.AddListener(PreviewAct);
                story.actEntered.AddListener(EnterAct);
                story.sequenceCancelled.AddListener(RestoreCommittedAct);
            }
            if (archive) EnterAct(archive.LatestReceivedAct);
            if (story) EnterAct(story.PresentedAct);
            RestoreCommittedAct();
        }

        /// <summary>Story entry is monotonic; inspecting older documents never replenishes wax.</summary>
        public void EnterAct(int act)
        {
            if (act < 1 || act > 5) return;
            EnteredAct = Mathf.Max(EnteredAct, act);
            RestoreCommittedAct();
        }

        void PreviewAct(int act)
        {
            if (act >= 1 && act <= 5) ApplyAct(Mathf.Max(EnteredAct, act));
        }

        void RestoreCommittedAct() => ApplyAct(EnteredAct);

        void ApplyAct(int act)
        {
            RemainingCount = 5 - act;
            for (int i = 0; i < candles.Length; i++)
                if (candles[i]) candles[i].SetActive(i < RemainingCount);
        }

        void OnDisable()
        {
            if (archive) archive.actReceived.RemoveListener(EnterAct);
            if (story)
            {
                story.actPrepared.RemoveListener(PreviewAct);
                story.actEntered.RemoveListener(EnterAct);
                story.sequenceCancelled.RemoveListener(RestoreCommittedAct);
            }
        }
    }
}
