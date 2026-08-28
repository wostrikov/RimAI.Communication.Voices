// RimAI.host-log: ALLOWED_HOST_LOG_SINK - the single place this module writes chatter to the host, so developer mode is the only switch for it.
using Verse;

namespace Ustas.RimAI.Communication.Voices.Diagnostics
{
    /// <summary>
    /// The module's running commentary, and the one place that decides whether
    /// anyone hears it.
    ///
    /// Developer mode is the switch, and it only ever governs chatter: cache
    /// updates, scheduling notices, parse previews - lines this module produced
    /// on every load whether or not anybody was reading them. Warnings and
    /// errors are deliberately absent from this class. A problem has to reach
    /// the player whether developer mode is on or not, so those calls stay on
    /// Verse.Log at their call sites, and hiding one behind a switch would be
    /// the opposite of what this project is for.
    /// </summary>
    internal static class ModuleLog
    {
        internal static void Message(string text)
        {
            if (Prefs.DevMode)
            {
                Log.Message(text);
            }
        }
    }
}
