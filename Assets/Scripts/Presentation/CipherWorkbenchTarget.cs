using UnityEngine;

namespace PurpleGlove
{
    /// <summary>A hit surface on a real prop. No screen-space controls or projected hit rectangles.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class CipherWorkbenchTarget : MonoBehaviour
    {
        public enum Kind { StoredTool, ReturnTool, Rotor, Slide, Cell, Plaintext, Keyword, PreviousKey, NextKey, Letter, CloseBox }
        public Kind kind;
        public CipherWorkbench.Tool tool = CipherWorkbench.Tool.None;
        public int index;
    }
}
