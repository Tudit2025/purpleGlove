using UnityEngine;

namespace PurpleGlove
{
    [DisallowMultipleComponent, RequireComponent(typeof(SceneObject))]
    public sealed class ActArchiveBundle : SceneObjectAction
    {
        public ActArchiveController archive;
        [Range(1, 5)] public int act = 1;

        public override void Activate()
        {
            if (archive) archive.ShowActOnDesk(act);
        }
    }
}
