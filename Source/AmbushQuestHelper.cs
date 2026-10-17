using RimWorld;
using RimWorld.QuestGen;
using RimWorld.Planet;
using Verse;
using System.Linq;

namespace TSA.AmbushQuest
{
    public static class AmbushQuestHelper
    {
        public const string QuestDefName = "TSA_AmbushCaravan_Quest";

        /// <summary>Returns true if a quest was generated and added.</summary>
        public static bool SpawnImmediateQuest()
        {
            var def = DefDatabase<QuestScriptDef>.GetNamedSilentFail(QuestDefName);
            if (def == null) return false;

            int foundTileID = -1;
            var homeMap = Find.AnyPlayerHomeMap;

            if (homeMap != null)
            {
                PlanetTile startTile = homeMap.Tile;
                for (int i = 0; i < 2000; i++)
                {
                    int t = TileFinder.RandomStartingTile();
                    float dist = Find.WorldGrid.ApproxDistanceInTiles(startTile, t);

                    if (dist >= 5 && dist <= 15)
                    {
                        if (TileFinder.IsValidTileForNewSettlement(t) && !Find.WorldObjects.AnyWorldObjectAt(t))
                        {
                            foundTileID = t;
                            break;
                        }
                    }
                }
            }

            if (foundTileID == -1) return false;

            Faction siteFaction = Find.FactionManager.RandomEnemyFaction() ?? Faction.OfPirates;
            var slate = new Slate();
            slate.Set("siteTile", new PlanetTile(foundTileID));
            slate.Set("siteFaction", siteFaction);

            var quest = QuestGen.Generate(def, slate);
            Find.QuestManager.Add(quest);

            Find.LetterStack.ReceiveLetter(
                "The Caravan Ambush",
                "An enemy caravan is passing through a nearby area. Intercept them!",
                LetterDefOf.NeutralEvent,
                new GlobalTargetInfo(foundTileID));

            return true;
        }

        public static void TryEndAmbush(PlanetTile tile, QuestEndOutcome outcome, string? message = null)
        {
            Quest quest = FindOngoingAmbushQuest(tile);
            if (quest == null) return;

            quest.End(outcome);

            if (!message.NullOrEmpty())
            {
                MessageTypeDef msgType = outcome == QuestEndOutcome.Success
                    ? MessageTypeDefOf.PositiveEvent
                    : MessageTypeDefOf.NegativeEvent;
                Messages.Message(message, msgType);
            }
        }

        public static Quest FindOngoingAmbushQuest(PlanetTile tile)
        {
            return Find.QuestManager.QuestsListForReading.FirstOrDefault(q =>
                q.root != null &&
                q.root.defName == QuestDefName &&
                q.State == QuestState.Ongoing &&
                QuestTargetsTile(q, tile));
        }

        private static bool QuestTargetsTile(Quest quest, PlanetTile tile)
        {
            foreach (GlobalTargetInfo t in quest.QuestLookTargets)
            {
                if (t.Tile == tile) return true;
            }
            return false;
        }
    }
}
