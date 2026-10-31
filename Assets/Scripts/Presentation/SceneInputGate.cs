using UnityEngine;

namespace PurpleGlove
{
    /// <summary>Shared modal state; the closing click/key never reaches the world underneath.</summary>
    [DisallowMultipleComponent]
    public sealed class SceneInputGate : MonoBehaviour
    {
        public bool DialogueActive { get; private set; }
        public bool ConsoleOpen { get; private set; }
        public bool WorkbenchModal { get; private set; }
        public bool WorkbenchTyping { get; private set; }
        public bool DeliveryActive { get; private set; }
        public bool StoryActive { get; private set; }
        public bool PropActionActive { get; private set; }
        public SceneObject DeliveryTarget { get; private set; }
        int ignoreThroughFrame = -1;
        public bool FrameConsumed => Time.frameCount <= ignoreThroughFrame;
        public bool BlocksObjectInput => PropActionActive || StoryActive || DialogueActive || ConsoleOpen || WorkbenchModal || WorkbenchTyping || Time.frameCount <= ignoreThroughFrame;
        public bool BlocksWorldInput => DeliveryActive || BlocksObjectInput;
        public bool CanInteract(SceneObject item) => !BlocksObjectInput && (!DeliveryActive || (item && item == DeliveryTarget));
        public bool BlocksDialogueInput => ConsoleOpen || Time.frameCount <= ignoreThroughFrame;
        public void SetDialogue(bool active) { DialogueActive = active; ConsumeFrame(); }
        public void SetStory(bool active) { StoryActive = active; ConsumeFrame(); }
        public void SetPropAction(bool active) { PropActionActive = active; ConsumeFrame(); }
        public void SetConsole(bool open) { ConsoleOpen = open; ConsumeFrame(); }
        public void SetWorkbenchModal(bool open) { WorkbenchModal = open; ConsumeFrame(); }
        public void SetWorkbenchTyping(bool active) { WorkbenchTyping = active; ConsumeFrame(); }
        public void SetDelivery(bool active, SceneObject target = null)
        {
            DeliveryActive = active; DeliveryTarget = active ? target : null; ConsumeFrame();
        }
        public void ConsumeFrame() => ignoreThroughFrame = Time.frameCount;
    }
}
