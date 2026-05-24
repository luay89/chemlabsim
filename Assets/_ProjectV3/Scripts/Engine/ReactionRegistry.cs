// ChemLabSim v3 — Reaction Registry
// Data-driven reaction lookup with O(1) indexed access.
// Loads from ReactionDB (populated from JSON by AppManager) and builds
// dictionary indices for fast reagent-key and ID lookups.
// Scalable: adding reactions to JSON automatically registers them.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChemLabSimV3.Engine
{
    public class ReactionRegistry
    {
        private readonly List<ReactionEntry> _all = new List<ReactionEntry>();
        private readonly Dictionary<string, ReactionEntry> _byKey = new Dictionary<string, ReactionEntry>();
        private readonly Dictionary<string, ReactionEntry> _byId  = new Dictionary<string, ReactionEntry>();

        public IReadOnlyList<ReactionEntry> All => _all;
        public int Count => _all.Count;

        public ReactionRegistry(ReactionDB db)
        {
            if (db?.reactions == null) return;

            for (int i = 0; i < db.reactions.Count; i++)
            {
                var r = db.reactions[i];
                if (r == null) continue;

                _all.Add(r);

                string key = BuildSortedKey(r.GetReactantFormulas());
                if (!string.IsNullOrEmpty(key) && !_byKey.ContainsKey(key))
                    _byKey[key] = r;

                if (!string.IsNullOrEmpty(r.id) && !_byId.ContainsKey(r.id))
                    _byId[r.id] = r;
            }

            Debug.Log($"[ReactionRegistry] Indexed {_all.Count} reactions ({_byKey.Count} keys, {_byId.Count} IDs).");
        }

        /// <summary>Find a reaction by its reagent formulas (order-independent, O(1)).</summary>
        public ReactionEntry Find(IEnumerable<string> reagentFormulas)
        {
            if (reagentFormulas == null) return null;

            string key = BuildSortedKey(reagentFormulas);
            if (string.IsNullOrEmpty(key)) return null;

            _byKey.TryGetValue(key, out ReactionEntry entry);
            return entry;
        }

        /// <summary>Find a reaction by its unique ID.</summary>
        public ReactionEntry FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            _byId.TryGetValue(id, out ReactionEntry entry);
            return entry;
        }

        /// <summary>Check if a reagent combination maps to any known reaction.</summary>
        public bool Contains(IEnumerable<string> reagentFormulas)
        {
            return Find(reagentFormulas) != null;
        }

        /// <summary>
        /// Check if there is a reaction that uses all the given reagents
        /// AND additional ones — indicating the user's selection is incomplete.
        /// </summary>
        public bool NeedsMoreReagents(IList<string> reagentFormulas)
        {
            if (reagentFormulas == null || reagentFormulas.Count < 2) return false;

            for (int i = 0; i < _all.Count; i++)
            {
                var rFormulas = _all[i].GetReactantFormulas();
                if (rFormulas.Count > reagentFormulas.Count &&
                    reagentFormulas.All(sel => rFormulas.Contains(sel)))
                    return true;
            }

            return false;
        }

        /// <summary>Build a canonical, order-independent key from a set of formulas.</summary>
        private static string BuildSortedKey(IEnumerable<string> formulas)
        {
            var sorted = formulas
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .OrderBy(x => x);

            return string.Join("|", sorted);
        }
    }
}
