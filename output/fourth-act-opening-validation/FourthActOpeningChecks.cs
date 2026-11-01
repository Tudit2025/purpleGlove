using System;
using System.Linq;
using PurpleGlove;
using UnityEngine;

public static partial class FourthActOpeningValidation
{
    static void FourthStep()
    {
        if (!dialogue.IsActive && dialogue.eyeShutter.IsMoving) Call(dialogue.eyeShutter, "Advance", 3f);
        Ungate(); Call(camera.GetComponent<FourthActOpening>(), "Advance");
    }
    static void VerifyFourthActOpening()
    {
        var opening = camera.GetComponent<FourthActOpening>();
        Check(opening && opening.archive == archive && opening.story == flow.story && opening.delivery == delivery, "Fourth opening wiring missing");
        Check(opening.State == FourthActOpening.Phase.WaitingForAct && !archive.IsAvailable(4), "Fourth opening ran before transition commit");
        Check(opening.introduction.TryValidate(out _) && dialogue.events.Count(e => e == opening.introduction) == 1, "Fourth introduction missing or duplicate");
        var papers = archive.acts[3].documents.ToArray();
        Check(papers.Length == 4 && papers.All(p => !p.gameObject.activeSelf) && archive.acts[3].deferredDocuments.Length == 0, "Expected four initially locked papers");
        Check(records.entries.Where(e => e.act == 4).All(e => e.sourceDocument == papers[2]), "Fourth ciphers do not require delivered letter");
        Check(!archive.ShowActOnDesk(4), "Shelf exposes fourth act before receipt");
        foreach (char c in string.Concat(opening.introduction.lines.Select(l => l.text)).Distinct().Where(c => !char.IsWhiteSpace(c)))
            Check(dialogue.Overlay.font.HasCharacter(c), "Missing fourth-act dialogue glyph " + c);
        archive.StoreDeskAct(); Ungate();
        // Previewing the dialogue alone must never count as opening or receipt.
        Check(dialogue.Play(opening.introduction), "Cannot preview fourth dialogue"); ReadAll();
        Check(opening.State == FourthActOpening.Phase.WaitingForAct && !archive.IsAvailable(4), "Preview advanced story");
        FourthStep(); Check(opening.State == FourthActOpening.Phase.IntroPending, "Fourth transition did not queue opening");
        gate.SetConsole(true); Call(opening, "Advance");
        Check(!dialogue.IsActive && opening.State == FourthActOpening.Phase.IntroPending, "Console failed to pause opening"); gate.SetConsole(false);
        FourthStep(); Check(dialogue.ActiveEvent == opening.introduction, "Missing fourth-act door introduction");
        OpenDialogue(); dialogue.Advance(); Capture("08-fourth-act-door-introduction");
        camera.GetComponent<DialogueTestConsole>().Execute("stop");
        Check(opening.State == FourthActOpening.Phase.Suspended && !dialogue.IsActive && !archive.IsAvailable(4), "Console stop did not suspend opening");
        camera.GetComponent<DialogueTestConsole>().Execute("continue"); FourthStep();
        Check(dialogue.ActiveEvent == opening.introduction, "Console continue did not resume opening");
        OpenDialogue();
        for (int i = 0; dialogue.IsActive && !dialogue.IsClosing && i < 100; i++) dialogue.Advance();
        Check(dialogue.IsClosing, "Introduction skipped shutter close");
        Ungate(); Call(opening, "Advance"); Check(!delivery.IsActive && !archive.IsAvailable(4), "Delivery starts before shutter closes");
        Call(dialogue.eyeShutter, "Advance", 3f); Call(dialogue, "AdvanceTime", 0f); FourthStep(); ReadyPacket();
        Check(delivery.fourthActPacketsVisual.activeSelf && !delivery.firstActPacketsVisual.activeSelf && !delivery.ordinaryPacketVisual.activeSelf, "Wrong package visuals");
        var pair = delivery.fourthActPacketsVisual.transform;
        var white = pair.Find("Act4_GuildArchivePacket").GetComponent<MeshFilter>();
        var brown = pair.Find("Act4_RepairedMasterPacket").GetComponent<MeshFilter>();
        Check(white && brown && white.sharedMesh.bounds.max.x < brown.sharedMesh.bounds.min.x, "Two packages overlap or lack visible gap");
        Check(!archive.IsAvailable(4) && papers.All(p => !p.gameObject.activeSelf), "Papers visible before collection");
        Call(view, "AnimateView", 3f, Vector2.zero); Capture("09-two-independent-packets");
        CapturePacketCloseup();
        opening.Stop(); Check(!archive.IsAvailable(4) && !delivery.IsActive && opening.Resume(), "Pickup cancellation unlocked papers");
        FourthStep(); ReadyPacket(); Check(delivery.CollectPacket(), "Collection rejected"); Call(delivery, "AdvanceTime", 0.4f);
        opening.Stop(); Check(!archive.IsAvailable(4) && papers.All(p => !p.gameObject.activeSelf) && opening.Resume(), "Flight cancellation unlocked papers");
        FourthStep(); ReadyPacket(); LandPacket();
        Check(opening.State == FourthActOpening.Phase.Complete && archive.IsAvailable(4) && archive.DeskAct == 4, "Fourth receipt did not complete");
        Check(received == 2 && deliveries == 3 && !delivery.BeginDelivery(4), "Fourth receipt duplicated");
        Check(UnityEngine.Object.FindFirstObjectByType<ActCandlePile>().RemainingCount == 1 && archive.CurrentAct == 4, "Receipt changes candle/act");
        string[] titles = { "商业文件委托签收页", "换版记录", "师父双栏密信", "师父的明文书信" };
        var mat = GameObject.Find("WritingMat").transform;
        for (int i = 0; i < papers.Length; i++)
        {
            Check(papers[i].name.EndsWith(titles[i]), "Unexpected paper " + papers[i].name);
            var box = papers[i].GetComponent<BoxCollider>();
            foreach (float x in new[] { -box.size.x / 2, box.size.x / 2 }) foreach (float z in new[] { -box.size.z / 2, box.size.z / 2 })
            {
                var p = mat.InverseTransformPoint(papers[i].transform.TransformPoint(new Vector3(x, 0, z)));
                Check(Mathf.Abs(p.x) < 0.49f && Mathf.Abs(p.z) < 0.54f, "Fourth paper outside desk mat");
            }
            Inspect(papers[i]); ValidatePaper(papers[i]); Capture((11 + i).ToString("00") + "-fourth-paper-" + (i + 1)); interactor.CancelInteraction();
        }
        string affine = papers[2].transform.Find("Affine cipher").GetComponent<TextMesh>().text;
        Check(affine == "VAPZR MIZC\n\nUIWVZ QIPZWV", "Affine word spaces or blank paragraph line missing");
        string decoded = string.Concat(affine.Where(char.IsLetter).Select(c => (char)('A' + ((21 * (c - 'A' - 8)) % 26 + 26) % 26)));
        Check(decoded == "NORTHGATESAINTMARTIN", "Affine cipher letters changed");
        string rail = string.Concat(papers[2].transform.Find("Rail cipher").GetComponent<TextMesh>().text.Where(char.IsLetter));
        int split = (rail.Length + 1) / 2;
        string decodedRail = string.Concat(Enumerable.Range(0, rail.Length).Select(i => rail[i % 2 == 0 ? i / 2 : split + i / 2]));
        Check(rail.Length == 82 && decodedRail == string.Concat(records.entries.Single(e => e.id == "rail").answer.Where(char.IsLetter)), "Rail cipher truncated or changed");
        string letter = papers[3].transform.Find("Body").GetComponent<TextMesh>().text;
        Check(letter.Contains("六点会转押") && letter.Contains("共同核验") && letter.Contains("与我无关的记录") && !letter.Contains("圣马丁"), "Plaintext letter missing content or reveals cipher answer");
        Record(); work.SetPlaintext("NORTH GATE SAINT MARTIN"); Record(); Record();
        Check(records.entries.Single(e => e.id == "affine").saved, "Delivered affine answer not preserved"); if (dialogue.IsActive) ReadAll();
        work.SetPlaintext(records.entries.Single(e => e.id == "rail").answer); Record(); Record();
        Check(records.entries.Single(e => e.id == "rail").saved, "Delivered rail answer not preserved"); if (dialogue.IsActive) ReadAll();
        var complete = archive.acts[3].documents.ToArray(); Check(complete.Length == 6, "Four originals and two plaintext records not retained");
        archive.StoreDeskAct(); archive.ShowActOnDesk(3); archive.ShowActOnDesk(4); Ungate();
        Check(complete.SequenceEqual(archive.acts[3].documents) && complete.All(p => p.gameObject.activeSelf), "Shelf recall lost fourth-act originals");
        Call(view, "AnimateView", 3f, Vector2.zero); Capture("15-complete-fourth-act-dossier");
        opening.enabled = false; opening.enabled = true; FourthStep();
        Check(opening.State == FourthActOpening.Phase.Complete && !dialogue.IsActive && !delivery.IsActive && deliveries == 3, "Fourth-act recall replays opening");
        // The new selection must leave other acts' visual variants working.
        Check(delivery.BeginDelivery(1), "Cannot test first-act variant");
        Check(delivery.firstActPacketsVisual.activeSelf && !delivery.fourthActPacketsVisual.activeSelf && !delivery.ordinaryPacketVisual.activeSelf, "First-act visual regressed"); delivery.Stop(); Ungate();
        Check(delivery.BeginDelivery(5), "Cannot test ordinary variant");
        Check(delivery.ordinaryPacketVisual.activeSelf && !delivery.firstActPacketsVisual.activeSelf && !delivery.fourthActPacketsVisual.activeSelf, "Ordinary packet visual regressed"); delivery.Stop();
    }

    static void CapturePacketCloseup()
    {
        var position = camera.transform.position; var rotation = camera.transform.rotation; float fov = camera.fieldOfView;
        var packet = delivery.packet.transform;
        try
        {
            var target = packet.TransformPoint(new Vector3(0, 0.02f, 0));
            camera.transform.position = target + (position - target).normalized * 0.62f;
            camera.transform.LookAt(target, Vector3.up); camera.fieldOfView = 38;
            Capture("10-two-packets-closeup");
        }
        finally { camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = fov; }
    }
}
