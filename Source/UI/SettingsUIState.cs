using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using Ustas.RimAI.Communication.Voices.Data;
using Ustas.RimAI.Communication.Voices.Service;
using Ustas.RimAI.Communication.Voices.Patch;

namespace Ustas.RimAI.Communication.Voices.UI
{
    internal static class SettingsUIState
    {
        internal static System.Collections.Concurrent.ConcurrentQueue<(string text, MessageTypeDef type)> pendingMessages = new System.Collections.Concurrent.ConcurrentQueue<(string, MessageTypeDef)>();
        internal static Vector2 scrollPosition = Vector2.zero;
        internal static Vector2 mainScrollPosition = Vector2.zero;
        internal static string processingPromptBuffer = "";
        internal static bool processingPromptInitialized = false;
        internal static string uploadPathBuffer = "";
        internal static string uploadNameBuffer = "";
        internal static string uploadTextBuffer = "";
        internal static System.Collections.Concurrent.ConcurrentQueue<System.Action> pendingActions = new System.Collections.Concurrent.ConcurrentQueue<System.Action>();
        internal static System.Collections.Generic.List<string> openAiModelChoices = new System.Collections.Generic.List<string>();
        internal static bool openAiModelsLoading = false;
        internal static bool showManualVoiceSection = false;
        internal static bool showRulesList = false;
        internal static int selectedRuleIndex = -1;
        internal static int lastClickedRuleIndex = -1;
        internal static float lastClickTime = 0f;
        internal static readonly float DOUBLE_CLICK_TIME = 0.5f;

        internal static void EnqueueMessage(string text, MessageTypeDef type)
        {
            pendingMessages.Enqueue((text, type));
        }

        internal static void EnqueueMainThreadAction(System.Action a)
        {
            if (a == null) return;
            pendingActions.Enqueue(a);
        }
    }
}
