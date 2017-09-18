﻿using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using System.Collections.Generic;
using System.IO;

namespace MidiAnim
{
    [ScriptedImporter(1, "midianim")]
    class MidiAnimImporter : ScriptedImporter
    {
        public float _bpm = 120;

        public override void OnImportAsset(AssetImportContext context)
        {
            var data = File.ReadAllBytes(context.assetPath);
            var song = MidiFileLoader.Load(data);

            var seq = new MidiTrackSequencer(song.tracks[0], song.division, _bpm);

            var noteCurves = new AnimationCurve[128];
            var ccCurves = new AnimationCurve[128];

            var beatCount = new AnimationCurve();
            var beatClock = new AnimationCurve();

            var barCount = new AnimationCurve();
            var barClock = new AnimationCurve();

            var beat = -1;

            for (var time = -1.0f;;)
            {
                const float deltaTime = 1.0f / 60;

                List<MidiEvent> events;

                if (time >= 0)
                {
                    events = seq.Advance(deltaTime);
                    time += deltaTime;
                }
                else
                {
                    events = seq.Start();
                    time = 0.0f;
                }

                var thisBeat = (int)(_bpm * time / 60);

                if (thisBeat != beat)
                {
                    beat = thisBeat;

                    beatCount.AddKey(time, beat);
                    if (time > 0)
                        beatClock.AddKey(time - deltaTime, 1);
                    beatClock.AddKey(time, 0);

                    if (beat % 4 == 0)
                    {
                        barCount.AddKey(time, beat / 4);
                        if (time > 0)
                            barClock.AddKey(time - deltaTime, 1);
                        barClock.AddKey(time, 0);
                    }
                }

                if (events != null)
                {
                    foreach (var e in events)
                    {
                        var pitch = e.data1;

                        if ((e.status & 0xf0) == 0x90)
                        {
                            if (noteCurves[pitch] == null)
                                noteCurves[pitch] = new AnimationCurve();
                            noteCurves[pitch].AddKey(time, 1);
                        }
                        else if ((e.status & 0xf0) == 0x80)
                        {
                            noteCurves[pitch].AddKey(time, 0);
                        }
                    }
                }

                if (!seq.Playing) break;
            }

            ModifyTangentsForCount(beatCount);
            ModifyTangentsForClock(beatClock);
            ModifyTangentsForCount(barCount);
            ModifyTangentsForClock(barClock);

            var clip = new AnimationClip();

            clip.SetCurve("", typeof(MidiState), "BeatCount", beatCount);
            clip.SetCurve("", typeof(MidiState), "BeatClock", beatClock);
            clip.SetCurve("", typeof(MidiState), "BarCount", barCount);
            clip.SetCurve("", typeof(MidiState), "BarClock", barClock);

            for (var i = 0; i < noteCurves.Length; i++)
                if (noteCurves[i] != null)
                {
                    ModifyTangentsForClock(noteCurves[i]);
                    clip.SetCurve("", typeof(MidiState), "Note[" + i + "]", noteCurves[i]);
                }

            context.SetMainAsset("MIDI", clip);
        }

        #region Animation curve utilities

        void ModifyTangentsForCount(AnimationCurve curve)
        {
            var tan = AnimationUtility.TangentMode.Constant;
            for (var i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, tan);
                AnimationUtility.SetKeyRightTangentMode(curve, i, tan);
            }
        }

        void ModifyTangentsForClock(AnimationCurve curve)
        {
            var ctan = AnimationUtility.TangentMode.Constant;
            var ltan = AnimationUtility.TangentMode.Linear;

            for (var i = 0; i < curve.length; i++)
            {
                var value = curve[i].value;
                if (value > 0.5f)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, ctan);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, ltan);
                }
                else
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, ltan);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, ctan);
                }
            }
        }

        #endregion
    }
}
