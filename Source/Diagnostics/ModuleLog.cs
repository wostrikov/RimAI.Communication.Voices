using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Communication.Voices.Diagnostics
{
    /// <summary>
    /// The module's running commentary, and the one place that decides where it
    /// goes.
    ///
    /// It no longer goes to RimWorld's debug log. That window stops accepting
    /// messages after a few hundred and says so - "Reached max messages limit.
    /// Stopping logging to avoid spam." - and this family's chatter reached
    /// that limit within two minutes of play, taking a real
    /// NullReferenceException with it. Chatter that consumes the channel real
    /// errors arrive on is worse than no chatter at all.
    ///
    /// Debug is the level, so developer mode is still the switch, and the host
    /// sink writes it to RimAI's own file. Warnings and errors are deliberately
    /// absent from this class: a problem has to reach the player whether
    /// developer mode is on or not, so those calls stay on Verse.Log at their
    /// call sites.
    /// </summary>
    internal static class ModuleLog
    {
        internal static void Message(string text)
        {
            RimAiLog.Debug(RimAiLogCategory.Voices, text);
        }
    }
}
