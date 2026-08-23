using System;
using System.Collections.Generic;
using System.IO;
using Ustas.RimAI.Communication.Voices.Policy;

internal static class PawnVoiceBindingTests
{
    public static int Run()
    {
        int n = 0;
        void T(bool x, string s)
        {
            if (!x)
                throw new Exception("FAILED " + s);
            n++;
        }

        T(PawnVoiceBindingPolicy.NormalizeAssignment(null) == PawnVoiceBindingPolicy.DefaultModelId, "empty-is-default");
        T(PawnVoiceBindingPolicy.Classify(PawnVoiceBindingPolicy.NoneModelId) == PawnVoiceBindingKind.Silent, "classify-none");
        T(PawnVoiceBindingPolicy.Classify("coral") == PawnVoiceBindingKind.Explicit, "classify-explicit");
        T(PawnVoiceBindingPolicy.ManualChoice(PawnVoiceBindingPolicy.DefaultModelId) == null, "manual-default-null");
        T(PawnVoiceBindingPolicy.ManualChoice("coral") == "coral", "manual-explicit");

        var map = new Dictionary<int, string>();
        PawnVoiceBindingPolicy.Assign(map, 1, "coral");
        PawnVoiceBindingPolicy.Assign(map, 2, "verse");
        T(PawnVoiceBindingPolicy.RawOrDefault(map, 1) == "coral", "alice-bound");
        T(PawnVoiceBindingPolicy.RawOrDefault(map, 2) == "verse", "bob-isolated");
        T(PawnVoiceBindingPolicy.RawOrDefault(map, 3) == PawnVoiceBindingPolicy.DefaultModelId, "unassigned-default");

        var alice = PawnVoiceBindingPolicy.ForDialogue(map[1], automaticEnabled: true);
        var bob = PawnVoiceBindingPolicy.ForDialogue(map[2], automaticEnabled: true);
        var muted = PawnVoiceBindingPolicy.ForDialogue(PawnVoiceBindingPolicy.NoneModelId, true);
        var auto = PawnVoiceBindingPolicy.ForDialogue(PawnVoiceBindingPolicy.DefaultModelId, true);
        T(!alice.Silent && alice.ExplicitVoiceId == "coral", "dialogue-alice");
        T(!bob.Silent && bob.ExplicitVoiceId == "verse", "dialogue-bob");
        T(alice.ExplicitVoiceId != bob.ExplicitVoiceId, "dialogue-isolated");
        T(muted.Silent, "dialogue-silent");
        T(auto.UseAutomatic && !auto.Silent, "dialogue-automatic");

        string manager = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "PawnVoiceManager.cs.src"));
        string renderer = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "PawnVoiceRenderer.cs.src"));
        string service = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TTSService.cs.src"));
        T(manager.Contains("PawnVoiceBindingPolicy.Assign"), "host-assign");
        T(manager.Contains("PawnVoiceBindingPolicy.RawOrDefault"), "host-raw");
        T(renderer.Contains("PawnVoiceBindingPolicy.ForDialogue"), "host-dialogue");
        T(service.Contains("PawnVoiceRenderer.Resolve"), "dialogue-uses-renderer");
        return n;
    }
}
