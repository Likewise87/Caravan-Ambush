using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using System.Linq;
using UnityEngine;

namespace TSA.AmbushQuest
{
    public class SitePartWorker_AmbushCaravan : SitePartWorker
    {
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            var settings = AmbushMod.settings;
            Faction enemyFaction = Find.FactionManager.RandomEnemyFaction() ?? Faction.OfPirates;

            IntVec3 playerStart = map.mapPawns.AllPawnsSpawned
                .FirstOrDefault(p => p.Faction != null && p.Faction.IsPlayer)?.Position ?? map.Center;

            if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && (c - playerStart).LengthHorizontal > 30f, map, CellFinder.EdgeRoadChance_Neutral, out IntVec3 entryCell))
                CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map), map, CellFinder.EdgeRoadChance_Neutral, out entryCell);

            if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && (c - entryCell).LengthHorizontal > map.Size.x * 0.7f, map, CellFinder.EdgeRoadChance_Neutral, out IntVec3 exitCell))
                exitCell = map.Center;

            var gc = Current.Game?.GetComponent<GameComponent_AmbushQuest>();
            int count = gc != null ? Mathf.Max(1, gc.questCount) : 1;
            float scale = Mathf.Pow(settings.scalingPerQuest, count - 1);
            if (scale < 1f) scale = 1f;

            int dynamicRadius = Mathf.RoundToInt(8f * scale);
            int guardCount = Mathf.RoundToInt(settings.pawnCount.RandomInRange * scale);
            int muffaloCount = Mathf.RoundToInt(settings.muffaloCount.RandomInRange * scale);

            List<Pawn> caravanPawns = new List<Pawn>();
            List<Pawn> muffalos = new List<Pawn>();

            for (int i = 0; i < guardCount; i++)
            {
                Pawn guard = PawnGenerator.GeneratePawn(PawnKindDef.Named("Mercenary_Gunner"), enemyFaction);
                GenSpawn.Spawn(guard, CellFinder.RandomClosewalkCellNear(entryCell, map, dynamicRadius), map);
                caravanPawns.Add(guard);
            }

            float totalWeight = settings.lootEntries.Where(x => x != null && x.def != null).Sum(e => e.weight);

            for (int i = 0; i < muffaloCount; i++)
            {
                Pawn muffalo = PawnGenerator.GeneratePawn(PawnKindDef.Named("Muffalo"), enemyFaction);
                GenSpawn.Spawn(muffalo, CellFinder.RandomClosewalkCellNear(entryCell, map, dynamicRadius), map);

                if (totalWeight > 0 && settings.lootEntries.Count > 0)
                {
                    int itemsToCarry = settings.muffaloItems.RandomInRange;

                    for (int j = 0; j < itemsToCarry; j++)
                    {
                        float roll = Rand.Range(0f, totalWeight);
                        float currentSum = 0f;
                        LootEntry selectedEntry = null;

                        foreach (var entry in settings.lootEntries)
                        {
                            if (entry == null || entry.def == null) continue;
                            currentSum += entry.weight;
                            if (roll <= currentSum)
                            {
                                selectedEntry = entry;
                                break;
                            }
                        }

                        if (selectedEntry != null && selectedEntry.def != null)
                        {
                            Thing t = ThingMaker.MakeThing(selectedEntry.def);
                            t.stackCount = selectedEntry.count;
                            muffalo.inventory.innerContainer.TryAdd(t);
                        }
                    }
                }
                caravanPawns.Add(muffalo);
                muffalos.Add(muffalo);
            }

            LordMaker.MakeNewLord(enemyFaction, new LordJob_AmbushTravel(exitCell), map, caravanPawns);
            foreach (Pawn p in caravanPawns) p.mindState.duty = new PawnDuty(DutyDefOf.TravelOrWait, exitCell);

            map.GetComponent<MapComponent_AmbushSite>()?.Init(muffalos);
        }
    }

    public class LordJob_AmbushTravel : LordJob
    {
        private IntVec3 destination;
        public LordJob_AmbushTravel() { }
        public LordJob_AmbushTravel(IntVec3 destination) => this.destination = destination;

        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();
            LordToil_Travel travel = new LordToil_Travel(destination);
            graph.AddToil(travel);
            LordToil_ExitMap exit = new LordToil_ExitMap(LocomotionUrgency.Walk, false);
            graph.AddToil(exit);
            LordToil_AssaultColony assault = new LordToil_AssaultColony();
            graph.AddToil(assault);

            Transition reachDestination = new Transition(travel, exit);
            reachDestination.AddTrigger(new Trigger_Memo("TravelArrived"));
            graph.AddTransition(reachDestination);

            Transition startAssault = new Transition(travel, assault);
            startAssault.AddTrigger(new Trigger_PawnDamagedCustom());
            startAssault.AddTrigger(new Trigger_PlayerNearbyOptimized(15f));
            graph.AddTransition(startAssault);

            Transition interruptExit = new Transition(exit, assault);
            interruptExit.AddTrigger(new Trigger_PawnDamagedCustom());
            graph.AddTransition(interruptExit);

            return graph;
        }

        public override void Notify_PawnLost(Pawn pawn, PawnLostCondition condition)
        {
            Map map = lord?.Map;
            map?.GetComponent<MapComponent_AmbushSite>()?.NotifyPawnLost(pawn, condition);
        }

        public override void ExposeData() => Scribe_Values.Look(ref destination, "destination");
    }

    public class Trigger_PlayerNearbyOptimized : Trigger
    {
        private float sqrRadius;
        public Trigger_PlayerNearbyOptimized(float radius) => this.sqrRadius = radius * radius;
        public override bool ActivateOn(Lord lord, TriggerSignal signal)
        {
            if (signal.type == TriggerSignalType.Tick && Find.TickManager.TicksGame % 120 == 0)
            {
                Pawn leader = lord.ownedPawns.FirstOrDefault();
                if (leader?.Map != null)
                    foreach (Pawn p in leader.Map.mapPawns.FreeColonistsSpawned)
                        if (!p.Dead && (leader.Position - p.Position).LengthHorizontalSquared < sqrRadius) return true;
            }
            return false;
        }
    }

    public class Trigger_PawnDamagedCustom : Trigger
    {
        public override bool ActivateOn(Lord lord, TriggerSignal signal) => signal.type == TriggerSignalType.PawnDamaged && signal.Pawn.Faction == lord.faction;
    }
}
