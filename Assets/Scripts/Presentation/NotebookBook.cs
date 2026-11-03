using System;
using UnityEngine;

namespace PurpleGlove
{
    /// <summary>A persistent reference book with physical page turns while being inspected.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SceneObject))]
    public sealed class NotebookBook : MonoBehaviour
    {
        [Serializable]
        public sealed class Spread
        {
            [TextArea(6, 20)] public string left;
            [TextArea(6, 20)] public string right;
        }

        public Spread[] spreads = Array.Empty<Spread>();
        public TextMesh leftPage, rightPage, leftNumber, rightNumber;
        public Transform turningLeaf;
        public TextMesh leafFront, leafBack;
        public GameObject pigpenChart;
        public int pigpenSpread = 3;
        public AudioSource pageAudio;
        public AudioClip pageTurnSound;
        [Min(0.1f)] public float turnSeconds = 0.48f;
        [SerializeField] int currentSpread;
        public int CurrentSpread => currentSpread;
        public int SpreadCount => spreads == null ? 0 : spreads.Length;
        public bool IsTurning { get; private set; }
        public bool CanGoBack => !IsTurning && currentSpread > 0;
        public bool CanGoForward => !IsTurning && currentSpread + 1 < SpreadCount;
        public bool CanTurnPages => isActiveAndEnabled && item && item.State == SceneObject.ObjectState.Inspecting
            && (!inputGate || !inputGate.BlocksWorldInput);

        SceneObject item;
        SceneInputGate inputGate;
        int targetSpread, direction;
        float elapsed;

        void Awake()
        {
            item = GetComponent<SceneObject>();
            if (Camera.main) inputGate = Camera.main.GetComponent<SceneInputGate>();
            // Pages fit their own sheet in SetPage, so the shared world-ink registry leaves them alone.
            foreach (var page in new[] { leftPage, rightPage, leafFront, leafBack }) Localization.Exclude(page);
            RefreshPages();
        }

        void OnEnable() => Localization.Changed += Relocalize;
        void Relocalize() { if (IsTurning) FinishTurn(); else RefreshPages(); }

        void Update()
        {
            if (!item || item.State != SceneObject.ObjectState.Inspecting)
            {
                if (IsTurning) FinishTurn();
                return;
            }
            if (Application.isFocused)
            {
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) HandlePageKey(KeyCode.RightArrow);
                else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) HandlePageKey(KeyCode.LeftArrow);
            }
            AdvanceTurn(Time.unscaledDeltaTime);
        }

        bool HandlePageKey(KeyCode key)
        {
            if (key == KeyCode.RightArrow || key == KeyCode.D) return TurnPage(1);
            if (key == KeyCode.LeftArrow || key == KeyCode.A) return TurnPage(-1);
            return false;
        }

        public bool TryTurnAtPointer(Camera camera, Vector2 screenPoint)
        {
            if (!camera || !item || item.State != SceneObject.ObjectState.Inspecting) return false;
            var ray = camera.ScreenPointToRay(screenPoint);
            var plane = new Plane(transform.up, transform.TransformPoint(new Vector3(0, 0.075f, 0)));
            if (!plane.Raycast(ray, out float distance)) return false;
            var point = transform.InverseTransformPoint(ray.GetPoint(distance));
            if (Mathf.Abs(point.z) > 0.225f || Mathf.Abs(point.x) > 0.31f || Mathf.Abs(point.x) < 0.035f) return false;
            return TurnPage(point.x > 0 ? 1 : -1);
        }

        public bool TurnPage(int step)
        {
            if (!CanTurnPages || IsTurning || (step != 1 && step != -1) || !turningLeaf) return false;
            int next = currentSpread + step;
            if (next < 0 || next >= SpreadCount) return false;
            direction = step;
            targetSpread = next;
            elapsed = 0;
            IsTurning = true;
            // The moving sheet carries the old face and the newly revealed reverse face.
            SetPage(leafFront, step > 0 ? spreads[currentSpread].right : spreads[next].right);
            SetPage(leafBack, step > 0 ? spreads[next].left : spreads[currentSpread].left);
            SetPage(leftPage, step > 0 ? spreads[currentSpread].left : spreads[next].left);
            SetPage(rightPage, step > 0 ? spreads[next].right : spreads[currentSpread].right);
            if (pigpenChart) pigpenChart.SetActive(false);
            turningLeaf.localRotation = Quaternion.Euler(0, 0, step > 0 ? 0 : 180);
            turningLeaf.gameObject.SetActive(true);
            if (pageAudio && pageTurnSound) pageAudio.PlayOneShot(pageTurnSound);
            return true;
        }

        void AdvanceTurn(float deltaTime)
        {
            if (!IsTurning) return;
            elapsed += deltaTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, turnSeconds));
            float angle = Mathf.SmoothStep(0, 180, progress);
            turningLeaf.localRotation = Quaternion.Euler(0, 0, direction > 0 ? angle : 180 - angle);
            if (progress >= 1) FinishTurn();
        }

        void FinishTurn()
        {
            currentSpread = targetSpread;
            IsTurning = false;
            RefreshPages();
        }

        void OnDisable()
        {
            Localization.Changed -= Relocalize;
            if (IsTurning) FinishTurn();
            if (pageAudio) pageAudio.Stop();
        }

        public void RefreshPages()
        {
            if (SpreadCount == 0) return;
            currentSpread = Mathf.Clamp(currentSpread, 0, SpreadCount - 1);
            SetPage(leftPage, spreads[currentSpread].left);
            SetPage(rightPage, spreads[currentSpread].right);
            if (leftNumber) leftNumber.text = (CanGoBack ? "<  " : "") + (currentSpread * 2 + 1).ToString("00");
            if (rightNumber) rightNumber.text = (currentSpread * 2 + 2).ToString("00") + (CanGoForward ? "  >" : "");
            if (pigpenChart) pigpenChart.SetActive(currentSpread == pigpenSpread);
            if (turningLeaf) turningLeaf.gameObject.SetActive(false);
        }

        static void SetPage(TextMesh text, string content)
        {
            if (!text) return;
            content = Localization.Text(content);
            text.text = content;
            text.characterSize = 0.0024f;
            text.font.RequestCharactersInTexture(content, text.fontSize);
            Vector3 size = text.GetComponent<MeshRenderer>().localBounds.size;
            float fit = Mathf.Min(1, 0.262f / Mathf.Max(size.x, 0.001f), 0.348f / Mathf.Max(size.y, 0.001f));
            text.characterSize *= fit;
        }
    }
}
