using System;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.UI;
using Ustas.RimAI.Communication.Voices.Data;
using Ustas.RimAI.Communication.Voices.Patch;
using Ustas.RimAI.Communication.Voices.Policy;
using Ustas.RimAI.Communication.Voices.Service;
using Ustas.RimAI.Core.Communication;
using Verse;

namespace Ustas.RimAI.Communication.Voices.Integration;

public static class TalkLifecycleBridge
{
    static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;
        _registered = true;
        TalkLifecycle.DisplayGate += OnDisplayGate;
        TalkLifecycle.TalkIgnored += OnTalkIgnored;
        TalkLifecycle.TalkResponseQueued += OnTalkResponseQueued;
        TalkLifecycle.GameSessionReset += OnGameSessionReset;
        TalkLifecycle.PlayerPawnInitialized += OnPlayerPawnInitialized;
        OverlayChrome.DrawAdjacentToGear += OverlayButtonPatch.Draw;
        OverlayChrome.TryConsumeClick += OverlayButtonPatch.TryConsumeClick;
        PersonaEditorChrome.DrawFooter += PersonaEditorPatch.DrawFooter;
    }

    static bool OnDisplayGate(object speaker, object talkResponse)
    {
        try
        {
            if (speaker is not Pawn pawn || talkResponse is not TalkResponse talk)
                return true;
            bool ttsActive = RimTalkPatches.TTSModuleIsActive();
            bool playing = AudioPlaybackService.IsCurrentlyPlaying();
            bool blocked = RimTalkPatches.IsBlocked(talk.Id);
            if (!DialogueAudioSyncPolicy.AllowDisplay(ttsActive, playing, blocked))
                return false;
            if (DialogueAudioSyncPolicy.ShouldStartPlayback(ttsActive, playing, blocked))
            {
                var settings = TTSConfig.Settings;
                float volume = settings?.GetSupplierVolume(settings.Supplier) ?? 1.0f;
                AudioPlaybackService.PlayAudio(talk.Id, pawn, volume);
            }
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"[RimAI.Voices] DisplayGate exception: {ex}");
            return true;
        }
    }

    static void OnTalkIgnored(string talkId)
    {
        if (!RimTalkPatches.TTSModuleIsActive())
            return;
        if (!Guid.TryParse(talkId, out var id))
            return;
        try
        {
            TTSModule.Instance.OnDialogueCancelled(id);
        }
        catch (Exception ex)
        {
            Log.Error($"[RimAI.Voices] TalkIgnored exception: {ex}");
        }
    }

    static void OnTalkResponseQueued(object speaker, object talkResponse)
    {
        try
        {
            if (!RimTalkPatches.TTSModuleIsActive())
                return;
            if (speaker is not Pawn pawn || talkResponse is not TalkResponse item)
                return;
            // Only a deliberately muted pawn is skipped here; the automatic path is
            // allowed to generate an identity later in the pipeline.
            if (PawnVoiceManager.GetRawVoiceModel(pawn) == VoiceModel.NONE_MODEL_ID)
                return;
            RimTalkPatches.RequestBlock(item.Id);
            TTSModule.Instance.OnDialogueGenerated(item.Text, pawn, item.Id);
        }
        catch (Exception ex)
        {
            Log.Error($"[RimAI.Voices] TalkResponseQueued exception: {ex}");
        }
    }

    static void OnGameSessionReset(string _)
    {
        TTSModule.Instance.OnGameLoaded();
        Log.Message("[RimAI.Voices] Game session reset, TTS state cleared");
    }

    static void OnPlayerPawnInitialized(object pawn)
    {
        RimTalkPatches.UpdatePlayerPawnVoice();
    }
}
