using Verse;
using RimWorld;
using UnityEngine;
using System.Linq;

namespace TSA.AmbushQuest
{
    public class GameComponent_AmbushQuest : GameComponent
    {
        private int nextQuestTick = -1;
        private bool initialized = false;

        /// <summary>Per-game ambush count used for scaling. Incremented only after a successful spawn.</summary>
        public int questCount = 0;

        public GameComponent_AmbushQuest(Game game) : base() { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextQuestTick, "nextQuestTick", -1);
            Scribe_Values.Look(ref initialized, "initialized", false);
            Scribe_Values.Look(ref questCount, "questCount", 0);
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 2000 != 0 || Find.AnyPlayerHomeMap == null) return;

            var settings = AmbushMod.settings;

            if (!initialized && nextQuestTick <= 0)
            {
                nextQuestTick = Find.TickManager.TicksGame + Mathf.RoundToInt(settings.daysFirst.RandomInRange * 60000f);
                initialized = true;
            }

            if (nextQuestTick > 0 && Find.TickManager.TicksGame >= nextQuestTick)
            {
                if (AmbushQuestHelper.SpawnImmediateQuest())
                {
                    questCount++;
                    nextQuestTick = -1;
                }
                else
                {
                    // No valid tile this pass — retry soon instead of parking forever.
                    nextQuestTick = Find.TickManager.TicksGame + 5000;
                }
            }

            if (nextQuestTick == -1)
            {
                bool questInProgress = Find.QuestManager.QuestsListForReading.Any(q =>
                    q.root != null &&
                    q.root.defName == AmbushQuestHelper.QuestDefName &&
                    q.State == QuestState.Ongoing);

                if (!questInProgress)
                    ResetTimer();
            }
        }

        public void ResetTimer()
        {
            nextQuestTick = Find.TickManager.TicksGame + Mathf.RoundToInt(AmbushMod.settings.daysSubsequent.RandomInRange * 60000f);
        }
    }
}
