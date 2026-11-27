// CMR (phase B): per Celeste save file Archipelago state.
using System.Collections.Generic;

namespace Celeste.Mod.UltraRando.AP {
    /// <summary>
    /// Stored by Everest inside the Celeste save file (one per save slot).
    /// A save file is bound to one seed + slot ("one save file per seed+slot"): once bound, the mod refuses
    /// to apply another seed's layout/items to it.
    /// </summary>
    public class ApSaveData : EverestModuleSaveData {
        /// <summary>Archipelago seed name (room seed) this save is bound to; empty = not bound.</summary>
        public string Seed { get; set; } = "";
        /// <summary>Slot name this save is bound to.</summary>
        public string SlotName { get; set; } = "";
        /// <summary>Number of received items already processed (AP ReceivedItems index).</summary>
        public int ReceivedIndex { get; set; } = 0;
        /// <summary>Names of the processed received items, in AP order (replayed when playing offline).</summary>
        public List<string> ReceivedItems { get; set; } = new List<string>();
        /// <summary>Every location id checked with this save (resent on reconnect; the server ignores duplicates).</summary>
        public List<long> CheckedLocations { get; set; } = new List<long>();
        /// <summary>Session state kept for good ("area|flag", "area|gem:N"): Oshiro clutter, Farewell breaker, summit
        /// gems. Put back in every new session (start, checkpoint, restart), as the logic counts them as permanent.</summary>
        public List<string> KeptState { get; set; } = new List<string>();

        public bool IsBound => !string.IsNullOrEmpty(Seed);

        public bool BoundTo(string seed, string slot) => Seed == seed && SlotName == slot;
    }
}
