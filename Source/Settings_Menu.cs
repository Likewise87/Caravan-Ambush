using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;

namespace TSA.AmbushQuest
{
    public class AmbushMod : Mod
    {
        public static Settings_Variables settings;
        public AmbushMod(ModContentPack content) : base(content) => settings = GetSettings<Settings_Variables>();

        public override string SettingsCategory() => "Caravan Ambush";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard l = new Listing_Standard();
            l.Begin(inRect);

            SettingsUI.DrawHeader(l, "Quest Timings (Days)");
            SettingsUI.RangeSliderFloat(l, "First Occurrence", ref settings.daysFirst, 0.1f, 60f,
                "The range of days after starting a game before the first caravan ambush quest can appear.",
                "3.0 – 5.0");
            SettingsUI.RangeSliderFloat(l, "Subsequent Intervals", ref settings.daysSubsequent, 1f, 120f,
                "The range of days between quests after the last one has been completed or expired.",
                "10.0 – 30.0");

            SettingsUI.DrawHeader(l, "Caravan Composition");
            SettingsUI.RangeSliderInt(l, "Enemy Guards", ref settings.pawnCount, 1, 100,
                "The base range of armed guards protecting the caravan. This value is multiplied by the Scaling Multiplier.",
                "5 – 10");
            SettingsUI.RangeSliderInt(l, "Loot Muffalos", ref settings.muffaloCount, 1, 40,
                "The base range of pack animals carrying loot. This value is multiplied by the Scaling Multiplier.",
                "2 – 4");
            SettingsUI.RangeSliderInt(l, "Item Stacks per Muffalo", ref settings.muffaloItems, 1, 3,
                "How many different types of item stacks a single Muffalo can carry in its inventory.",
                "1 – 2");

            SettingsUI.DrawHeader(l, "Caravan Size Scaling");
            settings.scalingPerQuest = SettingsUI.LabeledSlider(l, "Scaling Multiplier", settings.scalingPerQuest, 1.0f, 2.0f,
                "Each time a quest occurs, the number of Caravan Pawns and muffalos is multiplied by this value cumulatively.",
                0.05f, (settings.scalingPerQuest * 100f).ToString("F0") + "%", "120%");

            SettingsUI.DrawHeader(l, "Loot Table");
            Rect configRect = l.GetRect(30f).LeftPart(0.4f);
            if (Widgets.ButtonText(configRect, "Open Loot Configurator..."))
            {
                Find.WindowStack.Add(new Dialog_LootConfig(settings));
            }
            TooltipHandler.TipRegion(configRect, "Open the sub-menu to add, remove, or adjust weights for items found on the Muffalos.");

            l.Gap(20f);
            SettingsUI.DrawResetButton(l, "Reset all to Defaults", () =>
            {
                settings.ResetToDefaults();
                Messages.Message("All settings reset to defaults.", MessageTypeDefOf.CautionInput, false);
            }, "Wipe all current settings and return to the original mod defaults (Steel, Components, Plasteel).");

            l.End();
            settings.Write();
        }
    }

    public class Dialog_LootConfig : Window
    {
        private Settings_Variables settings;
        private string searchKey = "";
        private Vector2 lootScrollPos = Vector2.zero;
        private Vector2 searchScrollPos = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(650f, 700f);

        public Dialog_LootConfig(Settings_Variables settings)
        {
            this.settings = settings;
            this.doCloseButton = true;
            this.absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (settings.lootEntries == null) settings.lootEntries = new List<LootEntry>();

            // Fixed regions so neither list can draw over the close button.
            const float closeReserve = 50f;
            const float addSectionHeight = 225f;
            float contentBottom = inRect.yMax - closeReserve;
            Rect addRect = new Rect(inRect.x, contentBottom - addSectionHeight, inRect.width, addSectionHeight);
            Rect topRect = new Rect(inRect.x, inRect.y, inRect.width, Mathf.Max(40f, addRect.y - inRect.y));

            // --- Upper: loot table ---
            Listing_Standard headerList = new Listing_Standard();
            headerList.Begin(topRect);
            SettingsUI.DrawHeader(headerList, "Muffalo Loot Configurator");
            headerList.Gap(2f);
            float headerUsed = headerList.CurHeight;
            headerList.End();

            float totalWeight = settings.lootEntries.Where(x => x != null && x.def != null).Sum(x => x.weight);
            if (totalWeight <= 0) totalWeight = 1f;

            const float rowH = 26f;
            Rect lootOutRect = new Rect(topRect.x, topRect.y + headerUsed, topRect.width, Mathf.Max(40f, topRect.height - headerUsed));
            int validCount = settings.lootEntries.Count(x => x != null && x.def != null);
            Rect lootViewRect = new Rect(0f, 0f, lootOutRect.width - 16f, Mathf.Max(lootOutRect.height, validCount * rowH));

            Widgets.BeginScrollView(lootOutRect, ref lootScrollPos, lootViewRect);
            float y = 0f;
            for (int i = settings.lootEntries.Count - 1; i >= 0; i--)
            {
                var entry = settings.lootEntries[i];
                if (entry == null || entry.def == null) { settings.lootEntries.RemoveAt(i); continue; }

                Rect row = new Rect(0f, y, lootViewRect.width, 24f);
                y += rowH;

                const float deleteSize = 18f;
                const float deletePadRight = 5f;
                Rect deleteRect = new Rect(row.xMax - deletePadRight - deleteSize, row.y + (row.height - deleteSize) * 0.5f, deleteSize, deleteSize);

                float pct = (entry.weight / totalWeight) * 100f;
                string weightLabel = $"{entry.weight:F0} ({pct:F0}%)".Colorize(Color.cyan);
                Widgets.Label(row.LeftPart(0.4f), $"{entry.def.LabelCap}: {weightLabel}");

                float sliderLeft = row.width * 0.4f + 4f;
                Rect sliderRect = new Rect(sliderLeft, row.y, deleteRect.x - 6f - sliderLeft, row.height);
                entry.weight = Widgets.HorizontalSlider(sliderRect, entry.weight, 0f, 100f, false, null, null, null, 1f);
                TooltipHandler.TipRegion(sliderRect, $"Adjust the relative weight of {entry.def.label}. Higher weights mean this item is more likely to be selected.");

                if (Widgets.ButtonImage(deleteRect, Widgets.CheckboxOffTex))
                    settings.lootEntries.RemoveAt(i);
                TooltipHandler.TipRegion(deleteRect, "Remove this item from the loot table.");
            }
            Widgets.EndScrollView();

            // --- Lower: search + scrollable results (fixed box, never past closeReserve) ---
            Listing_Standard addHeader = new Listing_Standard();
            addHeader.Begin(addRect);
            SettingsUI.DrawHeader(addHeader, "Add New Items");
            Rect searchRect = addHeader.GetRect(28f);
            searchKey = Widgets.TextField(searchRect, searchKey);
            TooltipHandler.TipRegion(searchRect, "Type the name of any item in the game to add it to the caravan's loot table.");
            addHeader.Gap(5f);
            float addHeaderUsed = addHeader.CurHeight;
            addHeader.End();

            Rect searchOutRect = new Rect(addRect.x, addRect.y + addHeaderUsed, addRect.width, Mathf.Max(24f, addRect.height - addHeaderUsed));

            if (!searchKey.NullOrEmpty() && settings.lootEntries.Count < 20)
            {
                var matches = DefDatabase<ThingDef>.AllDefs
                    .Where(x => x.category == ThingCategory.Item && !x.IsCorpse && !x.IsBlueprint && !x.IsFrame &&
                                x.label != null && x.label.ToLower().Contains(searchKey.ToLower()) &&
                                !settings.lootEntries.Any(e => e.def == x))
                    .Take(40)
                    .ToList();

                Rect searchViewRect = new Rect(0f, 0f, searchOutRect.width - 16f, Mathf.Max(searchOutRect.height, matches.Count * rowH));
                Widgets.BeginScrollView(searchOutRect, ref searchScrollPos, searchViewRect);
                float sy = 0f;
                foreach (var def in matches)
                {
                    Rect matchRect = new Rect(0f, sy, searchViewRect.width, 24f);
                    sy += rowH;
                    if (Widgets.ButtonText(matchRect, $"Add {def.LabelCap}"))
                    {
                        settings.lootEntries.Add(new LootEntry(def, 20f, def.stackLimit > 1 ? (def.stackLimit / 2) : 1));
                        searchKey = "";
                    }
                }
                Widgets.EndScrollView();
            }
        }
    }

    public static class SettingsUI
    {
        public static readonly Color SectionHeaderColor = new Color(0.55f, 0.85f, 1f);

        public static void DrawHeader(Listing_Standard l, string label, Color? color = null)
        {
            l.Gap(12f);
            Color c = color ?? SectionHeaderColor;
            string hex = ColorUtility.ToHtmlStringRGBA(c);
            string styledLabel = $"<size=15><b><color=#{hex}>{label}</color></b></size>";
            float h = Mathf.Max(24f, Text.CalcHeight(styledLabel, l.ColumnWidth) + 2f);
            TextAnchor prev = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(l.GetRect(h), styledLabel);
            Text.Anchor = prev;
            l.GapLine(4f);
            l.Gap(4f);
        }

        public static void DrawResetButton(Listing_Standard l, string label, System.Action onReset, string tooltip = null)
        {
            if (onReset == null) return;
            Rect resetRect = l.GetRect(30f).LeftPart(0.3f);
            if (!tooltip.NullOrEmpty()) TooltipHandler.TipRegion(resetRect, tooltip);
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.5f, 0.5f);
            if (Widgets.ButtonText(resetRect, label)) onReset();
            GUI.color = prev;
        }

        public static float LabeledSlider(Listing_Standard l, string label, float val, float min, float max, string tooltip = null, float step = 0.05f, string displaySuffix = null, string defaultText = null)
        {
            l.Gap(2f);
            Rect r = l.GetRect(24f);
            if (!tooltip.NullOrEmpty()) TooltipHandler.TipRegion(r, TooltipWithDefault(tooltip, defaultText));
            string suffix = displaySuffix ?? val.ToString("F1");
            Widgets.Label(r.LeftPart(0.5f), $"{label}: {suffix.Colorize(Color.cyan)}");
            return Widgets.HorizontalSlider(r.RightPart(0.5f), val, min, max, false, null, null, null, step);
        }

        public static void RangeSliderFloat(Listing_Standard l, string label, ref FloatRange range, float min, float max, string tooltip = null, string defaultText = null)
        {
            l.Gap(2f);
            Rect r = l.GetRect(24f);
            if (!tooltip.NullOrEmpty()) TooltipHandler.TipRegion(r, TooltipWithDefault(tooltip, defaultText));
            string rangeDisplay = $"{range.min:F1} - {range.max:F1}";
            Widgets.Label(r.LeftPart(0.5f), $"{label}: {rangeDisplay.Colorize(Color.cyan)}");
            Widgets.FloatRange(r.RightPart(0.5f), label.GetHashCode(), ref range, min, max);
        }

        public static void RangeSliderInt(Listing_Standard l, string label, ref IntRange range, int min, int max, string tooltip = null, string defaultText = null)
        {
            l.Gap(2f);
            Rect r = l.GetRect(24f);
            if (!tooltip.NullOrEmpty()) TooltipHandler.TipRegion(r, TooltipWithDefault(tooltip, defaultText));
            string rangeDisplay = $"{range.min} - {range.max}";
            Widgets.Label(r.LeftPart(0.5f), $"{label}: {rangeDisplay.Colorize(Color.cyan)}");
            Widgets.IntRange(r.RightPart(0.5f), label.GetHashCode(), ref range, min, max);
        }

        private static string TooltipWithDefault(string tooltip, string defaultText)
        {
            if (defaultText.NullOrEmpty()) return tooltip;
            return AppendDefault(tooltip, defaultText);
        }

        private static string AppendDefault(string tooltip, string def)
        {
            string line = $"Default: {def}";
            return tooltip.NullOrEmpty() ? line : tooltip + "\n" + line;
        }
    }
}
