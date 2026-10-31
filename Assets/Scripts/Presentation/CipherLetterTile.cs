using UnityEngine;

namespace PurpleGlove
{
    /// <summary>Stable A-Z identity and a separate animation pivot for later performances.</summary>
    [DisallowMultipleComponent]
    public sealed class CipherLetterTile : MonoBehaviour
    {
        [Range(0, 25)] public int alphabetIndex;
        public Transform animationPivot;
        public Transform homeSocket;
        public char Letter => (char)('A' + alphabetIndex);
        public bool IsReturning { get; private set; }
        Vector3 origin;
        Quaternion originRotation;
        float elapsed;

        public void ReturnHome(bool immediate = false)
        {
            if (!homeSocket) return;
            origin = transform.position; originRotation = transform.rotation; elapsed = 0;
            IsReturning = !immediate;
            if (immediate) transform.SetPositionAndRotation(homeSocket.position, homeSocket.rotation);
        }

        public void StopMotion() => IsReturning = false;

        void Update()
        {
            if (!IsReturning || !homeSocket) return;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.22f), eased = Mathf.SmoothStep(0, 1, t);
            transform.SetPositionAndRotation(Vector3.Lerp(origin, homeSocket.position, eased) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.025f),
                Quaternion.Slerp(originRotation, homeSocket.rotation, eased));
            if (t >= 1) IsReturning = false;
        }
    }
}
