using UnityEngine;

namespace PurpleGlove
{
    /// <summary>Optional primary action for a prop that does more than open the inspection view.</summary>
    public abstract class SceneObjectAction : MonoBehaviour
    {
        public abstract void Activate();
        public virtual bool IsBusy => false;
        public virtual void Cancel() { }
    }
}
