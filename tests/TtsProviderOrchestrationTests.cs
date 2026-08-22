using System;
using System.Collections.Generic;
using System.IO;
using Ustas.RimAI.Communication.Voices.Policy;

internal static class TtsProviderOrchestrationTests
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

        T(!TtsProviderChain.RequiresCredential(TtsProviderKind.EdgeTts), "edge-keyless");
        T(TtsProviderChain.RequiresCredential(TtsProviderKind.OpenAi), "openai-keyed");

        var openAiWithKey = TtsProviderChain.Build(TtsProviderKind.OpenAi, true);
        T(openAiWithKey.Count == 2, "openai-then-edge");
        T(openAiWithKey[0].Kind == TtsProviderKind.OpenAi, "preferred-first");
        T(openAiWithKey[1].Kind == TtsProviderKind.EdgeTts && openAiWithKey[1].IsKeyless, "edge-fallback");

        var openAiNoKey = TtsProviderChain.Build(TtsProviderKind.OpenAi, false);
        T(openAiNoKey.Count == 1 && openAiNoKey[0].Kind == TtsProviderKind.EdgeTts, "missing-key-uses-edge");

        var edgeOnly = TtsProviderChain.Build(TtsProviderKind.EdgeTts, false);
        T(edgeOnly.Count == 1 && edgeOnly[0].Kind == TtsProviderKind.EdgeTts, "edge-preferred-no-dup");

        var none = TtsProviderChain.Build(TtsProviderKind.None, false);
        T(none.Count == 1 && none[0].Kind == TtsProviderKind.None, "none-is-terminal");

        T(TtsFailureClassifier.Classify(401, false, false) == TtsFailureClass.Auth, "classify-auth");
        T(TtsFailureClassifier.Classify(429, false, false) == TtsFailureClass.Transient, "classify-429");
        T(TtsFailureClassifier.Classify(null, true, false) == TtsFailureClass.Cancelled, "classify-cancel");
        T(TtsFailureClassifier.Classify(200, false, true) == TtsFailureClass.Transient, "classify-empty");

        var attempts = new List<TtsProviderKind>();
        var failover = TtsProviderOrchestrator.Execute(openAiWithKey, slot =>
        {
            attempts.Add(slot.Kind);
            if (slot.Kind == TtsProviderKind.OpenAi)
                return new TtsSlotResult { Class = TtsFailureClass.Transient };
            return new TtsSlotResult { Class = TtsFailureClass.Success, Audio = new byte[] { 1, 2, 3 } };
        });
        T(failover.Class == TtsFailureClass.Success && failover.UsedKind == TtsProviderKind.EdgeTts, "failover-to-edge");
        T(attempts.Count == 2, "failover-two-attempts");

        var authStop = TtsProviderOrchestrator.Execute(openAiWithKey, slot =>
            new TtsSlotResult { Class = TtsFailureClass.Auth });
        T(authStop.Class == TtsFailureClass.Auth && authStop.Attempts == 1, "auth-no-fanout");

        var cancelStop = TtsProviderOrchestrator.Execute(openAiWithKey, slot =>
            new TtsSlotResult { Class = TtsFailureClass.Cancelled });
        T(cancelStop.Class == TtsFailureClass.Cancelled && cancelStop.Attempts == 1, "cancel-no-fanout");

        var exhausted = TtsProviderOrchestrator.Execute(openAiWithKey, slot =>
            new TtsSlotResult { Class = TtsFailureClass.Transient });
        T(exhausted.Class == TtsFailureClass.Exhausted, "exhausted");

        string service = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TTSService.cs.src"));
        T(service.Contains("TtsProviderChain.Build"), "service-builds-chain");
        T(service.Contains("TtsProviderOrchestrator.ExecuteAsync"), "service-uses-orchestrator");
        T(service.Contains("OpenAITtsCredential"), "service-tts-credential-domain");
        return n;
    }
}
