using System.Linq;
using PurpleGlove;
using UnityEngine;

public static partial class OperationalRackValidation
{
    static void CheckStowed(DeskOperationalDocument form)
    {
        Check(!form.IsOpen && form.gameObject.activeSelf && form.rackTab.activeSelf, "Delivered form is not stowed: " + form.name);
        Check(form.transform.parent == form.storageAnchor && form.transform.localPosition.sqrMagnitude < 0.000001f
            && Quaternion.Angle(form.transform.localRotation, Quaternion.identity) < 0.01f
            && Vector3.Distance(form.transform.localScale, form.storageScale) < 0.0001f, "Form not in its original rack position");
        Check(!archive.acts.Any(a => form.transform.IsChildOf(a.storageAnchor)), "Working form entered an act shelf");
    }
    static void VerifyFirstActRack()
    {
        var forms = UnityEngine.Object.FindObjectsOfType<DeskOperationalDocument>(true);
        Check(forms.Length == 7, "Expected transfer, verification, three replies, custody and final checklist");
        Check(forms.All(f => !f.Available && !f.gameObject.activeSelf), "Undelivered operational forms visible");
        var transfer = forms.Single(f => f.receivedInAct == 1);
        Check(dialogue.events.Single(e => e.id == "act1_arrival").lines.Count(l => l.text.Contains("档案转交申请表")) == 1, "Application provenance missing or repeated");
        Check(delivery.BeginDelivery(1), "Cannot receive first-act application"); ReadyPacket();
        Check(!transfer.Available && !transfer.gameObject.activeSelf, "Application unlocked before pickup"); LandPacket();
        Check(archive.acts[0].documents.Length == 4 && transfer.Available, "First act lacks supplementary application");
        CheckStowed(transfer);
        var originalCamera = camera.transform.position;
        var originalRotation = camera.transform.rotation;
        Inspect(transfer.GetComponent<SceneObject>());
        Check(transfer.IsOpen && transfer.IsReady && !transfer.rackTab.activeSelf, "Form did not unfold");
        Check(Vector3.Distance(transfer.transform.position, transfer.desktopAnchor.position) < 0.001f
            && Quaternion.Angle(transfer.transform.rotation, transfer.desktopAnchor.rotation) < 0.05f
            && Vector3.Distance(transfer.transform.localScale, transfer.desktopScale) < 0.001f, "Form is floating instead of lying on the mat");
        Check(view.StoryLocked && !gate.StoryActive && interactor.InspectedObject == transfer.GetComponent<SceneObject>(), "Form interaction locks are wrong");
        ValidatePaper(transfer.GetComponent<SceneObject>()); Capture("16-transfer-application-open");
        // Close through the same Esc input path as the player.
        Ungate(); Call(interactor, "ProcessInput", Vector2.zero, false, false, true);
        Call(transfer.GetComponent<SceneObject>(), "Animate", 3f); Call(interactor, "ProcessInput", Vector2.zero, false, false, false);
        CheckStowed(transfer); Check(!view.StoryLocked && !interactor.InspectedObject, "Esc leaked interaction/view lock");
        Inspect(transfer.GetComponent<SceneObject>());
        camera.GetComponent<DialogueTestConsole>().SetOpen(true);
        CheckStowed(transfer); Check(!view.StoryLocked && !interactor.InspectedObject, "Console left form expanded");
        camera.GetComponent<DialogueTestConsole>().SetOpen(false); Ungate();
        Inspect(transfer.GetComponent<SceneObject>());
        archive.StoreDeskAct(); CheckStowed(transfer);
        Check(!view.StoryLocked && archive.DeskAct == 0, "Archive switch did not close active form");
        archive.ShowActOnDesk(1); CheckStowed(transfer);
        Check(forms.Where(f => f.receivedInAct > 1).All(f => !f.Available && !f.gameObject.activeSelf), "First receipt unlocked later forms");
        Call(view, "AnimateView", 3f, Vector2.zero); Capture("17-first-act-application-in-rack");
    }

    static void VerifyFinalRack()
    {
        var forms = UnityEngine.Object.FindObjectsOfType<DeskOperationalDocument>(true);
        foreach (var form in forms.Where(f => f.receivedInAct <= 3)) CheckStowed(form);
        Check(flow.verification.Signed && flow.verification.signatureInk.text == "Luca", "Cross-act stowing lost signed verification");
        Check(forms.Where(f => f.receivedInAct == 5).All(f => !f.Available && !f.gameObject.activeSelf), "Final forms unlocked early");
        Check(archive.CompleteAct(4), "Cannot enter fifth act");
        Check(delivery.BeginDelivery(5), "Cannot receive fifth-act working forms"); ReadyPacket(); LandPacket();
        foreach (var form in forms) CheckStowed(form);
        Check(archive.acts[4].documents.Count(d => !d.GetComponent<DeskOperationalDocument>()) == 3, "Fifth act has unexpected ordinary papers");
        foreach (var form in forms)
        {
            Inspect(form.GetComponent<SceneObject>());
            Check(Vector3.Distance(form.transform.position, form.desktopAnchor.position) < 0.001f, "Opened form left writing mat");
            var another = forms.First(f => f != form);
            interactor.Inspect(another.GetComponent<SceneObject>());
            Check(!another.IsOpen && interactor.InspectedObject == form.GetComponent<SceneObject>(), "Two operational forms opened together");
            interactor.CancelInteraction(); CheckStowed(form); Check(!view.StoryLocked, "Forced cancel leaked camera lock");
        }
        archive.StoreDeskAct();
        foreach (var form in forms) CheckStowed(form);
        foreach (int act in new[] { 1, 3, 4, 5 })
        {
            Check(archive.ShowActOnDesk(act), "Cannot recall act " + act);
            foreach (var form in forms) CheckStowed(form);
        }
        var verification = flow.verification.GetComponent<DeskOperationalDocument>();
        Inspect(verification.GetComponent<SceneObject>()); Capture("18-signed-verification-on-desk"); interactor.CancelInteraction();
        archive.StoreDeskAct(); Call(view, "AnimateView", 3f, Vector2.zero); Capture("19-operational-forms-in-rack");
        var rack = GameObject.Find("CorrespondenceRack").transform;
        var position = camera.transform.position; var rotation = camera.transform.rotation; float fov = camera.fieldOfView;
        try
        {
            var target = rack.TransformPoint(new Vector3(0, 0.22f, 0));
            camera.transform.position = target + (position - target).normalized * 0.95f;
            camera.transform.LookAt(target, Vector3.up); camera.fieldOfView = 44;
            Capture("20-rack-closeup");
            Physics.SyncTransforms();
            foreach (var form in forms)
            {
                var targetTab = form.rackTab.transform.TransformPoint(new Vector3(0, 0.008f, 0));
                var ray = new Ray(camera.transform.position, (targetTab - camera.transform.position).normalized);
                Check(Physics.Raycast(ray, out var hit, 5f) && hit.collider.GetComponentInParent<SceneObject>() == form.GetComponent<SceneObject>(),
                    "Rack tab is occluded: " + form.storageAnchor.name);
            }
        }
        finally { camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = fov; }
    }
}
