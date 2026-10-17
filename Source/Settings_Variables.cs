using Verse;
using System.Collections.Generic;
using RimWorld;

namespace TSA.AmbushQuest
{
    public class LootEntry : IExposable
    {
        private string defName; // Storage for the string identifier
        private ThingDef cachedDef; // Local cache to avoid repeated lookups

        public float weight;
        public int count;

        // The actual ThingDef getter used by the rest of the mod
        public ThingDef def
        {
            get
            {
                if (cachedDef == null && !defName.NullOrEmpty())
                {
                    cachedDef = GenDefDatabase.GetDefSilentFail(typeof(ThingDef), defName) as ThingDef;
                }
                return cachedDef;
            }
            set
            {
                cachedDef = value;
                defName = value?.defName;
            }
        }

        public LootEntry() { }
        public LootEntry(ThingDef def, float weight, int count)
        {
            this.def = def;
            this.weight = weight;
            this.count = count;
        }

        public void ExposeData()
        {
            // We save and load the string "defName" instead of the Def reference
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref weight, "weight", 10f);
            Scribe_Values.Look(ref count, "count", 1);
        }
    }

    public class Settings_Variables : ModSettings
    {
        // Timings
        public FloatRange daysFirst = new FloatRange(3f, 5f);
        public FloatRange daysSubsequent = new FloatRange(10f, 30f);

        // Counts
        public IntRange pawnCount = new IntRange(5, 10);
        public IntRange muffaloCount = new IntRange(2, 4);
        public IntRange muffaloItems = new IntRange(1, 2); // New: Items per muffalo

        // Scaling
        public float scalingPerQuest = 1.2f;

        // Flexible Loot List
        public List<LootEntry> lootEntries = new List<LootEntry>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref daysFirst, "daysFirst", new FloatRange(3f, 5f));
            Scribe_Values.Look(ref daysSubsequent, "daysSubsequent", new FloatRange(10f, 30f));
            Scribe_Values.Look(ref pawnCount, "pawnCount", new IntRange(5, 10));
            Scribe_Values.Look(ref muffaloCount, "muffaloCount", new IntRange(2, 4));
            Scribe_Values.Look(ref muffaloItems, "muffaloItems", new IntRange(1, 2));
            Scribe_Values.Look(ref scalingPerQuest, "scalingPerQuest", 1.3f);
            Scribe_Collections.Look(ref lootEntries, "lootEntries", LookMode.Deep);
        }

        public void ResetToDefaults()
        {
            daysFirst = new FloatRange(3f, 5f);
            daysSubsequent = new FloatRange(10f, 30f);
            pawnCount = new IntRange(5, 10);
            muffaloCount = new IntRange(2, 4);
            muffaloItems = new IntRange(1, 2);
            scalingPerQuest = 1.2f;
            ResetLootToDefault();
        }

        private void ResetLootToDefault()
        {
            lootEntries = new List<LootEntry>
            {
                new LootEntry(ThingDefOf.Steel, 40f, 150),
                new LootEntry(ThingDefOf.ComponentIndustrial, 20f, 15),
                new LootEntry(ThingDefOf.Plasteel, 20f, 50)
            };
        }
    }
}