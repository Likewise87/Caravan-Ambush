using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace TSA.AmbushQuest
{
    public class MapComponent_AmbushSite : MapComponent
    {
        private List<Pawn> trackedMuffalos = new List<Pawn>();
        private PlanetTile siteTile;
        private bool questEnded;

        public MapComponent_AmbushSite(Map map) : base(map) { }

        public void Init(IEnumerable<Pawn> muffalos)
        {
            trackedMuffalos = muffalos?.Where(p => p != null).ToList() ?? new List<Pawn>();
            siteTile = map.Tile;
            questEnded = false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref trackedMuffalos, "trackedMuffalos", LookMode.Reference);
            Scribe_Values.Look(ref siteTile, "siteTile");
            Scribe_Values.Look(ref questEnded, "questEnded", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && trackedMuffalos == null)
                trackedMuffalos = new List<Pawn>();
        }

        public override void MapComponentTick()
        {
            if (questEnded || trackedMuffalos.Count == 0) return;
            if (Find.TickManager.TicksGame % 60 != 0) return;
            TryResolveFromState();
        }

        public override void MapRemoved()
        {
            if (questEnded) return;
            if (AllMuffalosDead())
                End(QuestEndOutcome.Success, "All caravan pack animals have been killed. The ambush succeeded.");
            else
                End(QuestEndOutcome.Fail, "The ambush failed — at least one pack animal survived.");
        }

        public void NotifyPawnLost(Pawn pawn, PawnLostCondition condition)
        {
            if (questEnded || pawn == null || !IsTracked(pawn)) return;

            if (pawn.Dead)
            {
                TryResolveFromState();
                return;
            }

            // Downed / still on map is not failure — only a living muffalo leaving the map.
            if (pawn.Spawned && pawn.Map == map) return;
            if (condition != PawnLostCondition.ExitedMap && condition != PawnLostCondition.Vanished)
                return;

            End(QuestEndOutcome.Fail, "A caravan pack animal escaped. The ambush failed.");
        }

        private void TryResolveFromState()
        {
            if (questEnded) return;
            if (!AllMuffalosDead()) return;
            End(QuestEndOutcome.Success, "All caravan pack animals have been killed. The ambush succeeded.");
        }

        private bool IsTracked(Pawn pawn) => trackedMuffalos.Contains(pawn);

        private bool AllMuffalosDead()
        {
            if (trackedMuffalos.Count == 0) return false;
            for (int i = 0; i < trackedMuffalos.Count; i++)
            {
                Pawn p = trackedMuffalos[i];
                if (p == null || !p.Dead) return false;
            }
            return true;
        }

        private void End(QuestEndOutcome outcome, string message)
        {
            if (questEnded) return;
            questEnded = true;
            AmbushQuestHelper.TryEndAmbush(siteTile, outcome, message);
        }
    }
}
