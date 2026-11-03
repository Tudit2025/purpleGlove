using System;
using UnityEngine;

namespace PurpleGlove
{
    /// <summary>One intro, a sample-continuous loop, then a boundary-scheduled outro.</summary>
    public sealed class StoryMusicVoice
    {
        const double Lead = 0.12;
        readonly AudioSource intro, loop, outro;
        StoryMusicSettings.Cue cue;
        double origin, frozen, scheduledElapsed, outroAt = -1;
        bool running, paused;
        public float Gain { get; set; }
        public bool IsPaused => paused;
        public bool IsFinishing => outroAt >= 0;
        public double Elapsed => !running ? 0 : paused ? frozen : Math.Max(scheduledElapsed, AudioSettings.dspTime - origin);
        public double OutroAt => outroAt;
        public bool HasFinished => running && outroAt >= 0 && Elapsed >= outroAt + Duration(cue.outro);
        static double Duration(AudioClip clip) => clip ? (double)clip.samples / clip.frequency : 0;

        public StoryMusicVoice(Transform parent, string name)
        {
            var root = new GameObject(name); root.transform.SetParent(parent, false);
            AudioSource Source()
            {
                var source = root.AddComponent<AudioSource>();
                source.playOnAwake = false; source.spatialBlend = 0; source.volume = 0;
                source.dopplerLevel = 0; source.priority = 128;
                return source;
            }
            intro = Source(); loop = Source(); outro = Source();
        }

        public void Play(StoryMusicSettings.Cue next)
        {
            Stop(); cue = next;
            if (cue == null || !cue.loop) return;
            running = true; Schedule(0);
        }

        void Queue(AudioSource source, AudioClip clip, double offset, double at, bool repeat = false)
        {
            if (!clip) return;
            source.clip = clip; source.loop = repeat;
            source.timeSamples = Mathf.Clamp((int)(offset * clip.frequency), 0, clip.samples - 1);
            source.PlayScheduled(at);
        }

        void Schedule(double elapsed)
        {
            intro.Stop(); loop.Stop(); outro.Stop();
            double now = AudioSettings.dspTime + Lead;
            origin = now - elapsed; scheduledElapsed = elapsed;
            double introLength = Duration(cue.intro), loopLength = Duration(cue.loop);
            if (elapsed < introLength)
                Queue(intro, cue.intro, elapsed, now);
            if (outroAt < 0 || (elapsed < outroAt && outroAt > introLength))
            {
                double position = Math.Max(0, elapsed - introLength) % loopLength;
                Queue(loop, cue.loop, position, now + Math.Max(0, introLength - elapsed), true);
                if (outroAt >= 0) loop.SetScheduledEndTime(origin + outroAt);
            }
            if (outroAt >= 0 && elapsed < outroAt + Duration(cue.outro))
                Queue(outro, cue.outro, Math.Max(0, elapsed - outroAt), now + Math.Max(0, outroAt - elapsed));
        }

        public void FinishAtBoundary()
        {
            if (!running || IsFinishing || !cue.outro) return;
            double elapsed = Elapsed, introLength = Duration(cue.intro), loopLength = Duration(cue.loop);
            outroAt = elapsed < introLength - Lead ? introLength
                : introLength + (Math.Floor(Math.Max(0, elapsed + Lead - introLength) / loopLength) + 1) * loopLength;
            if (!paused)
            {
                if (outroAt == introLength) loop.Stop();
                else loop.SetScheduledEndTime(origin + outroAt);
                Queue(outro, cue.outro, 0, origin + outroAt);
            }
        }

        public void SetPaused(bool value)
        {
            if (!running || value == paused) return;
            if (value)
            {
                frozen = Elapsed; intro.Stop(); loop.Stop(); outro.Stop(); paused = true;
            }
            else { paused = false; Schedule(frozen); }
        }

        public void SetVolume(float volume)
        {
            intro.volume = loop.volume = outro.volume = Mathf.Clamp01(volume * Gain);
        }

        public void Stop()
        {
            intro.Stop(); loop.Stop(); outro.Stop();
            running = paused = false; outroAt = -1; frozen = 0; Gain = 0;
        }
    }
}
