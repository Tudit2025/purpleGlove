using System;
using UnityEngine;

namespace PurpleGlove
{
    /// <summary>A once-per-play recollection before a prop's ordinary interaction.</summary>
    [DefaultExecutionOrder(-250), DisallowMultipleComponent, RequireComponent(typeof(SceneObject))]
    public sealed class FirstInteractionMemory : MonoBehaviour
    {
        public DialogueController dialogue;
        public DialogueEvent memory;
        [Tooltip("Keep an inspected document in view while Luca recalls its details.")]
        public bool duringInspection;
        public bool HasRemembered { get; private set; }
        public bool IsRemembering { get; private set; }
        SceneInputGate gate;
        Action continueInteraction;
        int completedFrame;
        bool awaitingContinuation;

        public bool TryBegin(Action next)
        {
            if (!isActiveAndEnabled || HasRemembered || !dialogue || !memory || !memory.TryValidate(out _)) return false;
            if (IsRemembering) return true;
            gate = dialogue.GetComponent<SceneInputGate>();
            if (!gate || gate.StoryActive || gate.DeliveryActive || gate.ConsoleOpen || dialogue.IsActive) return false;
            continueInteraction = next;
            dialogue.eventCompleted.AddListener(Completed);
            dialogue.eventCancelled.AddListener(Cancelled);
            if (dialogue.PlayInPlace(memory, duringInspection)) { IsRemembering = true; return true; }
            ClearPending(); return false;
        }

        void Completed(string id)
        {
            if (!IsRemembering || !memory || id != memory.id) return;
            HasRemembered = true; IsRemembering = false;
            Unsubscribe(); completedFrame = Time.frameCount; awaitingContinuation = true;
        }
        void Cancelled(string id)
        {
            if (IsRemembering && memory && id == memory.id) ClearPending();
        }

        void Update()
        {
            if (!awaitingContinuation || Time.frameCount <= completedFrame) return;
            if (!dialogue || !dialogue.isActiveAndEnabled || !gate || gate.StoryActive || gate.DeliveryActive || dialogue.IsActive)
            { ClearPending(); return; }
            if (gate.BlocksWorldInput) return;
            var item = GetComponent<SceneObject>();
            var next = continueInteraction;
            ClearPending();
            if (!item || !item.isActiveAndEnabled) return;
            next?.Invoke();
            // The key/click that finishes the line must not flip a page or pick a toolbox slot.
            gate.ConsumeFrame();
        }

        void Unsubscribe()
        {
            if (!dialogue) return;
            dialogue.eventCompleted.RemoveListener(Completed);
            dialogue.eventCancelled.RemoveListener(Cancelled);
        }
        void ClearPending()
        {
            Unsubscribe(); IsRemembering = awaitingContinuation = false; continueInteraction = null;
        }
        void OnDisable()
        {
            bool ownsDialogue = IsRemembering && dialogue && dialogue.ActiveEvent == memory;
            ClearPending();
            if (ownsDialogue) dialogue.Stop();
        }
    }
}
