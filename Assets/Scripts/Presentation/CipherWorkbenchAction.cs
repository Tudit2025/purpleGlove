using UnityEngine;

namespace PurpleGlove
{
    public sealed class CipherWorkbenchAction : SceneObjectAction
    {
        public enum ActionKind { Toolbox, Focus }
        public CipherWorkbench workbench;
        public ActionKind action;
        public override void Activate()
        {
            if (!workbench) return;
            if (action == ActionKind.Toolbox) workbench.OpenToolbox();
            else workbench.FocusWorkspace();
        }
    }
}
