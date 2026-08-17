using Verse;

namespace Ustas.RimAI.Communication.Voices.Data
{
    /// <summary>
    /// Hooks per-save voice state into the save/load cycle: generated pawn voice
    /// identities plus the legacy manual assignments.
    /// </summary>
    public class PawnVoiceGameComponent : GameComponent
    {
        public PawnVoiceGameComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            PawnVoiceManager.ExposeData();
            Voice.PawnVoiceIdentityStore.ExposeData();
        }
    }
}