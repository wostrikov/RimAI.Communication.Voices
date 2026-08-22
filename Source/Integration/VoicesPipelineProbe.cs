using System;
using System.Collections.Generic;
using RimWorld;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Service;
using Ustas.RimAI.Communication.Voices.Data;
using Ustas.RimAI.Communication.Voices.Patch;
using Ustas.RimAI.Communication.Voices.Policy;
using Ustas.RimAI.Communication.Voices.Service;
using Ustas.RimAI.Core.Communication;
using Ustas.RimAI.Core.TestDriver;
using Verse;

namespace Ustas.RimAI.Communication.Voices.Integration;

/// <summary>
/// Live bubble/audio ordering. Blocks a fixture line, injects silent WAV,
/// then opens the display gate. Does not call a paid TTS provider.
/// </summary>
public static class VoicesPipelineProbe
{
    public static void Register()
    {
        TestDriverModuleOperations.Register(
            TestDriverCommandNames.ProbeVoices,
            (request, _) => new TestDriverDelegateOperation(() => Run(request)));
    }

    static TestDriverProgress Run(TestDriverRequest request)
    {
        if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null)
            return TestDriverProgress.Failed("probe_voices requires a loaded game");

        var mode = request.Arguments.GetString("mode", "audio_sync");
        var correlationId = request.Arguments.GetString("correlationId", request.RequestId);
        if (!string.Equals(mode, "audio_sync", StringComparison.OrdinalIgnoreCase))
            return TestDriverProgress.Failed("mode must be audio_sync");

        return AudioSync(correlationId);
    }

    static TestDriverProgress AudioSync(string correlationId)
    {
        var pawn = PickPawn();
        if (pawn == null)
            return TestDriverProgress.Failed("no spawned colonist");

        var settings = TTSConfig.Settings;
        if (settings == null)
            return TestDriverProgress.Failed("TTS settings are not initialized");

        bool previousEnable = settings.EnableTTS;
        bool previousButton = settings.isOnButton;
        var talk = new TalkResponse(TalkType.Other, pawn.LabelShort, "Probe opening. No provider call.");
        var cancelId = Guid.NewGuid();
        var pawnState = Cache.Get(pawn);
        var savedOthers = new List<KeyValuePair<PawnState, List<TalkResponse>>>();
        int playLogBefore = Find.PlayLog?.AllEntries?.Count ?? 0;
        bool canDisplayBlocked = false;
        bool canDisplayReady = false;
        bool canDisplayCancelBlocked = false;
        bool cancelReleased = false;
        bool bubbleCreated = false;
        bool audioReadyBeforeBubble = false;
        string firstError = null;
        int ticksBlocked = 0;
        int ticksReady = 0;
        int ticksBubble = 0;

        try
        {
            settings.EnableTTS = true;
            settings.isOnButton = true;

            if (pawnState == null)
                return TestDriverProgress.Failed("colonist is not talk-eligible");

            foreach (var other in Cache.GetAll())
            {
                if (other == null || other == pawnState || other.TalkResponses.Count == 0)
                    continue;
                savedOthers.Add(new KeyValuePair<PawnState, List<TalkResponse>>(
                    other, new List<TalkResponse>(other.TalkResponses)));
                other.TalkResponses.Clear();
            }

            pawnState.TalkResponses.RemoveAll(item => item != null && item.Id == talk.Id);
            pawnState.TalkResponses.Insert(0, talk);

            RimTalkPatches.RequestBlock(talk.Id);
            RimTalkPatches.RequestBlock(cancelId);
            ticksBlocked = Find.TickManager?.TicksGame ?? 0;
            canDisplayBlocked = TalkLifecycle.CanDisplay(pawn, talk);
            TalkService.DisplayTalk(ignoreReplyInterval: true);
            int playLogWhileBlocked = Find.PlayLog?.AllEntries?.Count ?? 0;

            AudioPlaybackService.SetAudioResult(talk.Id, SilentWav());
            RimTalkPatches.ReleaseBlock(talk.Id);
            ticksReady = Find.TickManager?.TicksGame ?? 0;
            canDisplayReady = DialogueAudioSyncPolicy.AllowDisplay(
                RimTalkPatches.TTSModuleIsActive(),
                AudioPlaybackService.IsCurrentlyPlaying(),
                RimTalkPatches.IsBlocked(talk.Id));
            TalkService.DisplayTalk(ignoreReplyInterval: true);
            ticksBubble = Find.TickManager?.TicksGame ?? 0;
            int playLogAfter = Find.PlayLog?.AllEntries?.Count ?? 0;
            bubbleCreated = playLogAfter > playLogWhileBlocked;
            audioReadyBeforeBubble = !canDisplayBlocked && canDisplayReady && ticksReady <= ticksBubble;

            var cancelTalk = new TalkResponse(TalkType.Other, pawn.LabelShort, "Cancelled probe line.")
            {
                Id = cancelId
            };
            canDisplayCancelBlocked = TalkLifecycle.CanDisplay(pawn, cancelTalk);
            TTSService.CancelDialogue(cancelId);
            cancelReleased = !RimTalkPatches.IsBlocked(cancelId);
        }
        catch (NullReferenceException ex)
        {
            firstError = ex.GetType().Name + ": " + ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            firstError = ex.GetType().Name + ": " + ex.Message;
        }
        catch (ArgumentException ex)
        {
            firstError = ex.GetType().Name + ": " + ex.Message;
        }
        finally
        {
            settings.EnableTTS = previousEnable;
            settings.isOnButton = previousButton;
            RimTalkPatches.ReleaseBlock(talk.Id);
            RimTalkPatches.ReleaseBlock(cancelId);
            AudioPlaybackService.RemovePendingAudio(talk.Id);
            AudioPlaybackService.RemovePendingAudio(cancelId);
            pawnState?.TalkResponses.RemoveAll(item => item != null && (item.Id == talk.Id || item.Id == cancelId));
            for (int i = 0; i < savedOthers.Count; i++)
            {
                var pair = savedOthers[i];
                if (pair.Key == null)
                    continue;
                pair.Key.TalkResponses.AddRange(pair.Value);
            }
        }

        bool noBubbleBeforeAudio = !canDisplayBlocked && canDisplayReady && audioReadyBeforeBubble;
        return TestDriverProgress.Completed(new TestDriverJsonWriter()
            .Text("mode", "audio_sync")
            .Text("correlationId", correlationId)
            .Text("pawn", pawn.LabelShort)
            .Text("dialogueId", talk.Id.ToString())
            .Flag("ttsForcedOn", true)
            .Flag("canDisplayBlocked", canDisplayBlocked)
            .Flag("canDisplayReady", canDisplayReady)
            .Flag("noBubbleBeforeAudio", noBubbleBeforeAudio)
            .Flag("bubbleCreated", bubbleCreated)
            .Flag("audioReadyBeforeBubble", audioReadyBeforeBubble)
            .Flag("canDisplayCancelBlocked", canDisplayCancelBlocked)
            .Flag("cancelReleased", cancelReleased)
            .Flag("lineCancelledWithReject", !canDisplayCancelBlocked && cancelReleased)
            .Integer("playLogBefore", playLogBefore)
            .Integer("ticksBlocked", ticksBlocked)
            .Integer("ticksReady", ticksReady)
            .Integer("ticksBubble", ticksBubble)
            .Flag("EXCEPTION_PRESENT", firstError != null)
            .Text("firstError", firstError)
            .Flag("paused", Find.TickManager?.Paused ?? true)
            .Integer("ticksGame", Find.TickManager?.TicksGame ?? 0));
    }

    static Pawn PickPawn()
    {
        var colonists = Find.CurrentMap?.mapPawns?.FreeColonistsSpawned;
        if (colonists == null)
            return null;
        for (int i = 0; i < colonists.Count; i++)
        {
            var pawn = colonists[i];
            if (pawn != null && !pawn.Dead && pawn.Spawned)
                return pawn;
        }

        return null;
    }

    static byte[] SilentWav()
    {
        const int sampleRate = 8000;
        const int samples = 160;
        var data = new byte[44 + samples * 2];
        WriteAscii(data, 0, "RIFF");
        WriteInt32(data, 4, data.Length - 8);
        WriteAscii(data, 8, "WAVE");
        WriteAscii(data, 12, "fmt ");
        WriteInt32(data, 16, 16);
        WriteInt16(data, 20, 1);
        WriteInt16(data, 22, 1);
        WriteInt32(data, 24, sampleRate);
        WriteInt32(data, 28, sampleRate * 2);
        WriteInt16(data, 32, 2);
        WriteInt16(data, 34, 16);
        WriteAscii(data, 36, "data");
        WriteInt32(data, 40, samples * 2);
        return data;
    }

    static void WriteAscii(byte[] dest, int offset, string text)
    {
        for (int i = 0; i < text.Length; i++)
            dest[offset + i] = (byte)text[i];
    }

    static void WriteInt16(byte[] dest, int offset, short value)
    {
        dest[offset] = (byte)value;
        dest[offset + 1] = (byte)(value >> 8);
    }

    static void WriteInt32(byte[] dest, int offset, int value)
    {
        dest[offset] = (byte)value;
        dest[offset + 1] = (byte)(value >> 8);
        dest[offset + 2] = (byte)(value >> 16);
        dest[offset + 3] = (byte)(value >> 24);
    }
}
