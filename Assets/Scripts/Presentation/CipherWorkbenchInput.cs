using UnityEngine;
using UnityEngine.EventSystems;

namespace PurpleGlove
{
    public sealed partial class CipherWorkbench
    {
        CipherWorkbenchTarget dragTarget;
        CipherLetterTile heldLetter;
        Vector3 dragStart, letterOrigin;
        float lastAngle, dragAngle;
        int dragShift;
        int movingTool = -1;
        bool returningTool;
        Tool requestedTool;
        float transferTime;
        Vector3 transferStart, transferScale;
        Quaternion transferRotation;
        Vector3[] toolPositions;
        Quaternion[] toolRotations;

        void RememberToolPoses()
        {
            toolPositions = new Vector3[tools.Length]; toolRotations = new Quaternion[tools.Length];
            for (int i = 0; i < tools.Length; i++) if (tools[i])
            {
                toolPositions[i] = tools[i].transform.localPosition;
                toolRotations[i] = tools[i].transform.localRotation;
            }
        }

        void StartToolTransfer(int index, bool returning)
        {
            movingTool = index; returningTool = returning; transferTime = 0;
            var item = tools[index].transform;
            if (!returning)
            {
                ActiveTool = (Tool)index; ApplySelection();
                item.SetPositionAndRotation(storedTools[index].transform.position, storedTools[index].transform.rotation);
                item.localScale = storedTools[index].transform.lossyScale;
            }
            transferStart = item.position; transferRotation = item.rotation; transferScale = item.localScale;
            Play(returning ? returnSound : takeSound);
        }

        void AnimateToolTransfer(float delta)
        {
            if (movingTool < 0) return;
            transferTime += delta;
            float t = Mathf.Clamp01(transferTime / 0.30f), smooth = Mathf.SmoothStep(0, 1, t);
            var item = tools[movingTool].transform;
            var stored = storedTools[movingTool].transform;
            Vector3 destination = returningTool ? stored.position : mat.TransformPoint(toolPositions[movingTool]);
            Quaternion rotation = returningTool ? stored.rotation : mat.rotation * toolRotations[movingTool];
            item.SetPositionAndRotation(Vector3.Lerp(transferStart, destination, smooth) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.10f),
                Quaternion.Slerp(transferRotation, rotation, smooth));
            item.localScale = Vector3.Lerp(transferScale, returningTool ? stored.lossyScale : Vector3.one, smooth);
            if (t < 1) return;
            int finished = movingTool; movingTool = -1;
            item.localPosition = toolPositions[finished]; item.localRotation = toolRotations[finished]; item.localScale = Vector3.one;
            if (returningTool)
            {
                ActiveTool = Tool.None; ApplySelection();
                if (requestedTool != Tool.None) StartToolTransfer((int)requestedTool, false);
            }
        }

        void CancelManipulation()
        {
            if (heldLetter) heldLetter.ReturnHome();
            heldLetter = null; dragTarget = null;
            if (movingTool >= 0)
            {
                movingTool = -1; ActiveTool = requestedTool;
                for (int i = 0; i < tools.Length; i++) if (tools[i])
                {
                    tools[i].transform.localPosition = toolPositions[i];
                    tools[i].transform.localRotation = toolRotations[i]; tools[i].transform.localScale = Vector3.one;
                }
                ApplySelection();
            }
            if (gate && gate.WorkbenchModal && !ToolboxOpen) gate.SetWorkbenchModal(false);
        }

        void ProcessPhysicalInput()
        {
            if ((!InWorkspace && !ToolboxOpen) || Interrupted || !sceneCamera || !Application.isFocused) return;
            bool canPoint = Input.mousePresent && Cursor.lockState != CursorLockMode.Locked && sceneCamera.pixelRect.Contains(Input.mousePosition)
                && (!viewHud || !viewHud.BlocksPointer(Input.mousePosition))
                && (!EventSystem.current || !EventSystem.current.IsPointerOverGameObject());
            if (Input.GetMouseButtonDown(1))
            {
                if (dragTarget) CancelManipulation();
                else if (ToolboxOpen) CloseToolbox();
                else if (typing) EndWriting();
                else
                {
                    var target = canPoint ? PickTarget(Input.mousePosition) : null;
                    var tile = target ? target.GetComponent<CipherLetterTile>() : null;
                    if (tile) tile.ReturnHome(); else SelectTool(Tool.None);
                }
                if (gate) gate.ConsumeFrame();
                return;
            }
            if (movingTool >= 0) return;
            if (dragTarget)
            {
                if (!canPoint) { CancelManipulation(); return; }
                if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0)) EndDrag(Input.mousePosition);
                else DragAt(Input.mousePosition);
                return;
            }
            if (!canPoint || !Input.GetMouseButtonDown(0) || (gate && gate.FrameConsumed)) return;
            var hit = PickTarget(Input.mousePosition);
            if (hit) ActivateTarget(hit, Input.mousePosition);
            else
            {
                EndWriting();
                if (ToolboxOpen) CloseToolbox();
                else
                {
                    Physics.SyncTransforms();
                    if (Physics.Raycast(sceneCamera.ScreenPointToRay(Input.mousePosition), out var boxHit, 10)
                        && boxHit.collider.GetComponentInParent<CipherWorkbenchAction>()?.action == CipherWorkbenchAction.ActionKind.Toolbox) OpenToolbox();
                }
            }
        }

        CipherWorkbenchTarget PickTarget(Vector2 pointer, bool ignoreHeld = false)
        {
            Physics.SyncTransforms();
            var hits = Physics.RaycastAll(sceneCamera.ScreenPointToRay(pointer), 10, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (ignoreHeld && heldLetter && hit.transform.IsChildOf(heldLetter.transform)) continue;
                var target = hit.collider.GetComponent<CipherWorkbenchTarget>();
                if (!target) return null; // Real geometry occludes the controls behind it.
                bool boxPart = target.kind == CipherWorkbenchTarget.Kind.StoredTool || target.kind == CipherWorkbenchTarget.Kind.CloseBox;
                if (boxPart != ToolboxOpen) return null;
                return target;
            }
            return null;
        }

        void ActivateTarget(CipherWorkbenchTarget target, Vector2 pointer)
        {
            if (Interrupted || movingTool >= 0 || !target || !target.isActiveAndEnabled) return;
            ActivatePhysicalPart(target, pointer);
            if (gate) gate.ConsumeFrame();
        }

        void ActivatePhysicalPart(CipherWorkbenchTarget target, Vector2 pointer)
        {
            switch (target.kind)
            {
                case CipherWorkbenchTarget.Kind.StoredTool: SelectTool(target.tool); break;
                case CipherWorkbenchTarget.Kind.CloseBox: CloseToolbox(); break;
                case CipherWorkbenchTarget.Kind.ReturnTool: SelectTool(Tool.None); break;
                case CipherWorkbenchTarget.Kind.Plaintext: EndWriting(); writing = true; BeginTyping(); Play(paperSound); break;
                case CipherWorkbenchTarget.Kind.Keyword: EndWriting(); editingKeyword = true; BeginTyping(); break;
                case CipherWorkbenchTarget.Kind.PreviousKey: EndWriting(); StepKeyword(-1); break;
                case CipherWorkbenchTarget.Kind.NextKey: EndWriting(); StepKeyword(1); break;
                case CipherWorkbenchTarget.Kind.Cell:
                    EndWriting(); editingCell = target.index; BeginTyping(); break;
                case CipherWorkbenchTarget.Kind.Letter:
                case CipherWorkbenchTarget.Kind.Rotor:
                case CipherWorkbenchTarget.Kind.Slide:
                    EndWriting(); BeginDrag(target, pointer); break;
            }
        }

        bool DeskPoint(Vector2 pointer, out Vector3 point)
        {
            var plane = new Plane(mat.up, mat.position + mat.up * 0.035f);
            var ray = sceneCamera.ScreenPointToRay(pointer);
            if (plane.Raycast(ray, out float distance)) { point = ray.GetPoint(distance); return true; }
            point = default; return false;
        }

        void BeginDrag(CipherWorkbenchTarget target, Vector2 pointer)
        {
            if (!DeskPoint(pointer, out dragStart)) return;
            dragTarget = target; dragShift = target.kind == CipherWorkbenchTarget.Kind.Rotor ? CaesarShift : SlideShift;
            Vector3 local = tools[(int)(target.kind == CipherWorkbenchTarget.Kind.Rotor ? Tool.Caesar : Tool.SaintCyr)].transform.InverseTransformPoint(dragStart);
            lastAngle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg; dragAngle = 0;
            heldLetter = target.GetComponent<CipherLetterTile>();
            if (heldLetter) { heldLetter.StopMotion(); letterOrigin = heldLetter.transform.position; }
            if (gate) gate.SetWorkbenchModal(true);
        }

        void DragAt(Vector2 pointer)
        {
            if (!dragTarget || !DeskPoint(pointer, out var point)) return;
            if (heldLetter) heldLetter.transform.position = letterOrigin + (point - dragStart) + mat.up * 0.045f;
            else if (dragTarget.kind == CipherWorkbenchTarget.Kind.Rotor)
            {
                Vector3 local = tools[0].transform.InverseTransformPoint(point);
                if (new Vector2(local.x, local.z).sqrMagnitude < 0.0004f) return;
                float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                dragAngle += Mathf.DeltaAngle(lastAngle, angle); lastAngle = angle;
                SetCaesarShift(dragShift + Mathf.RoundToInt(dragAngle * 26 / 360));
            }
            else SetSlideShift(Mathf.Clamp(dragShift - Mathf.RoundToInt(Vector3.Dot(point - dragStart, mat.right) / slidePitch), 0, 25));
        }

        void EndDrag(Vector2 pointer)
        {
            DragAt(pointer);
            if (heldLetter)
            {
                var target = PickTarget(pointer, true);
                if (target && target.kind == CipherWorkbenchTarget.Kind.Cell)
                {
                    SetCell(ActiveTool, target.index, heldLetter.Letter.ToString()); heldLetter.ReturnHome(); Play(paperSound);
                }
                else if (target && target.kind == CipherWorkbenchTarget.Kind.Keyword)
                { SetKeyword(Keyword + heldLetter.Letter); heldLetter.ReturnHome(); }
                else if (target && target.kind == CipherWorkbenchTarget.Kind.Plaintext)
                { SetPlaintext(Plaintext + heldLetter.Letter); heldLetter.ReturnHome(); }
                else if (!target && DeskPoint(pointer, out var point))
                {
                    Vector3 local = mat.InverseTransformPoint(point);
                    // The front edge of the mat is a free composition strip, clear of the tool and writing slip.
                    if (Mathf.Abs(local.x) < 0.34f && local.z > -0.335f && local.z < -0.30f)
                        heldLetter.transform.position = mat.TransformPoint(new Vector3(local.x, 0.013f, -0.32f));
                    else heldLetter.ReturnHome();
                }
                else heldLetter.ReturnHome();
            }
            heldLetter = null; dragTarget = null;
            if (gate) gate.SetWorkbenchModal(false);
        }

        void UpdateQuill()
        {
            if (!writingQuill || !typing) return;
            Transform paper = editingCell >= 0 ? (ActiveTool == Tool.Affine ? affineLetters[editingCell] : railLetters[editingCell]).transform
                : editingKeyword && keywordInk ? keywordInk.transform : plaintextInk ? plaintextInk.transform : null;
            if (paper)
            {
                Quaternion rotation = Quaternion.Euler(-15, -35, 50);
                writingQuill.SetPositionAndRotation(paper.position + Vector3.up * 0.002f - rotation * new Vector3(-0.21f, 0.004f, 0), rotation);
                Vector3 screen = sceneCamera.WorldToScreenPoint(paper.position);
                Input.compositionCursorPos = new Vector2(screen.x, Screen.height - screen.y);
            }
        }

        // IMGUI receives key/IME events only. It draws no labels, buttons, fields, panels or overlays.
        void OnGUI()
        {
            if (Interrupted || !InWorkspace || Event.current.type != EventType.KeyDown) return;
            if (typing) HandleWritingInput(Event.current);
            else HandleDecodeKey(Event.current);
        }

        void HandleWritingInput(Event e)
        {
            if (e.keyCode == KeyCode.Escape) { EndWriting(); e.Use(); return; }
            if (editingCell >= 0) { HandleGridInput(e, ActiveTool == Tool.Affine ? 52 : 82); return; }
            string value = editingKeyword ? Keyword : Plaintext;
            if ((e.control || e.command) && e.keyCode == KeyCode.V) value += GUIUtility.systemCopyBuffer;
            else if (e.keyCode == KeyCode.Backspace) { if (value.Length > 0) value = value.Substring(0, value.Length - 1); }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            { if (editingKeyword) { EndWriting(); e.Use(); return; } value += "\n"; }
            else if (!e.control && !e.command && e.character >= ' ') value += e.character;
            else return;
            if (editingKeyword) SetKeyword(value); else SetPlaintext(value);
            e.Use();
        }

        void HandleGridInput(Event e, int length)
        {
            if (editingCell < 0 || e.type != EventType.KeyDown) return;
            int columns = length / 2;
            if ((e.control || e.command) && e.keyCode == KeyCode.V)
            {
                foreach (char c in LettersOnly(GUIUtility.systemCopyBuffer, length - editingCell))
                { SetCell(ActiveTool, editingCell, c.ToString()); editingCell = Mathf.Min(length - 1, editingCell + 1); }
            }
            else if (e.keyCode == KeyCode.Backspace) { SetCell(ActiveTool, editingCell, ""); editingCell = Mathf.Max(0, editingCell - 1); }
            else if (e.keyCode == KeyCode.Delete) SetCell(ActiveTool, editingCell, "");
            else if (e.keyCode == KeyCode.LeftArrow) editingCell = Mathf.Max(0, editingCell - 1);
            else if (e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.Tab) editingCell = (editingCell + (e.shift ? length - 1 : 1)) % length;
            else if (e.keyCode == KeyCode.UpArrow) editingCell = Mathf.Max(0, editingCell - columns);
            else if (e.keyCode == KeyCode.DownArrow) editingCell = Mathf.Min(length - 1, editingCell + columns);
            else if (e.keyCode == KeyCode.Return) { EndWriting(); e.Use(); return; }
            else if (!e.control && !e.command && char.IsLetter(e.character) && e.character <= 127)
            { SetCell(ActiveTool, editingCell, e.character.ToString()); editingCell = Mathf.Min(length - 1, editingCell + 1); }
            else return;
            e.Use();
        }
    }
}
