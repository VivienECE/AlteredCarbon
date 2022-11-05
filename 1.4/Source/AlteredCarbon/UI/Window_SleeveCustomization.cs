﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;
using Verse.Sound;
using static AlteredCarbon.UIHelper;

namespace AlteredCarbon
{
    [HotSwappable]
    public class Window_SleeveCustomization : Window
    {
        //Variables
        public Pawn curSleeve;
        private PawnKindDef currentPawnKindDef;
        private readonly Building_SleeveGrower sleeveGrower;
        private Xenogerm curXenogerm;
        private bool convertXenogenesToEndegones;
        private List<Gene> convertedGenes = new List<Gene>();

        private bool allowMales = true;
        private bool allowFemales = true;

        // UI variables
        private readonly float leftOffset = 20;
        private readonly float pawnSpacingFromEdge = 5;
        private int scrollHeightCount = 0;
        private Vector2 scrollPosition;

        // indexes for lists
        private Dictionary<string, int> indexesPerCategory;
        private int hairTypeIndex = 0;
        private int beardTypeIndex = 0;
        private int raceTypeIndex = 0;
        private int headTypeIndex = 0;
        private int maleBodyTypeIndex = 0;
        private int femaleBodyTypeIndex = 0;
        private int sleeveQualityIndex = 2;

        private int ticksToGrow = 900000;
        private int growCost = 250;
        private HediffDef qualityDiff;

        private static readonly List<HediffDef> sleeveQualities = new List<HediffDef>
        {
            AC_DefOf.VFEU_Sleeve_Quality_Awful,
            AC_DefOf.VFEU_Sleeve_Quality_Poor,
            AC_DefOf.VFEU_Sleeve_Quality_Normal,
            AC_DefOf.VFEU_Sleeve_Quality_Good,
            AC_DefOf.VFEU_Sleeve_Quality_Excellent,
            AC_DefOf.VFEU_Sleeve_Quality_Masterwork,
            AC_DefOf.VFEU_Sleeve_Quality_Legendary
        };

        public static Dictionary<HediffDef, int> sleeveQualitiesTimeCost = new Dictionary<HediffDef, int>
        {
            {AC_DefOf.VFEU_Sleeve_Quality_Awful, 0 },
            {AC_DefOf.VFEU_Sleeve_Quality_Poor, GenDate.TicksPerDay * 2 },
            {AC_DefOf.VFEU_Sleeve_Quality_Normal, GenDate.TicksPerDay * 3 },
            {AC_DefOf.VFEU_Sleeve_Quality_Good, GenDate.TicksPerDay * 5 },
            {AC_DefOf.VFEU_Sleeve_Quality_Excellent, GenDate.TicksPerDay * 10 },
            {AC_DefOf.VFEU_Sleeve_Quality_Masterwork, GenDate.TicksPerDay * 15 },
            {AC_DefOf.VFEU_Sleeve_Quality_Legendary, GenDate.TicksPerDay * 30 },
        };

        //Static Values
        [TweakValue("0AC", 0, 200)] public static float AlienRacesYOffset = 32;
        [TweakValue("0AC", 500, 1500)] public static float InitialWindowXSize = 728f;
        [TweakValue("0AC", 500, 1000)] public static float InitialWindowYSize = 690f;
        [TweakValue("0AC", 300, 500)] public static float LineSeparatorWidth = 500;

        public override Vector2 InitialSize
        {
            get
            {
                float xSize = 900;
                float ySize = UI.screenHeight;
                return new Vector2(xSize, ySize);
            }
        }

        public Window_SleeveCustomization(Building_SleeveGrower sleeveGrower)
        {
            this.sleeveGrower = sleeveGrower;
            Init(PawnKindDefOf.Colonist);
            InitUI();
        }

        public Window_SleeveCustomization(Building_SleeveGrower sleeveGrower, Pawn pawn)
        {
            this.sleeveGrower = sleeveGrower;
            Init(pawn.kindDef, pawn.gender);
            CopyBodyFrom(pawn);
            InitUI();
        }

        private void InitUI()
        {
            forcePause = true;
            absorbInputAroundWindow = true;
        }

        private void Init(PawnKindDef pawnKindDef, Gender? gender = null)
        {
            currentPawnKindDef = pawnKindDef; ;
            if (!gender.HasValue)
            {
                gender = Rand.Bool ? Gender.Male : Gender.Female;
            }
            CreateSleeve(gender.Value);
        }


        public override void DoWindowContents(Rect inRect)
        {

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "AC.SleeveCustomization".Translate());

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;

            Vector2 firstColumnPos = new Vector2(leftOffset, 50);
            Vector2 secondColumnPos = new Vector2(600, firstColumnPos.y);

            var outRect = new Rect(0, firstColumnPos.y, inRect.width, inRect.height - 250);
            var viewRect = new Rect(0, outRect.y, inRect.width - 30, scrollHeightCount);
            scrollHeightCount = 0;
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            Text.Anchor = TextAnchor.MiddleLeft;
            var genderLabel = "Gender".Translate() + ":";
            Rect genderRect = GetLabelRect(genderLabel, ref firstColumnPos);
            Widgets.Label(genderRect, genderLabel);
            Rect maleGenderRect = new Rect(genderRect.xMax + buttonOffsetFromText, genderRect.y, buttonWidth, buttonHeight);
            Rect femaleGenderRect = new Rect(maleGenderRect.xMax + buttonOffsetFromButton, genderRect.y, buttonWidth, buttonHeight);
            if (allowMales && Widgets.ButtonText(maleGenderRect, "Male".Translate().CapitalizeFirst()))
            {
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                CreateSleeve(Gender.Male);
            }
            if (allowFemales && Widgets.ButtonText(femaleGenderRect, "Female".Translate().CapitalizeFirst()))
            {
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                CreateSleeve(Gender.Female);
            }

            if (Widgets.ButtonText(femaleGenderRect, "Female".Translate().CapitalizeFirst()))
            {
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                CreateSleeve(Gender.Female);
            }

            var label = "SelectXenogerm".Translate() + ":";
            Rect labelRect = GetLabelRect(label, ref firstColumnPos);
            Widgets.Label(labelRect, label);
            Rect highlightRect = new Rect(labelRect.xMax + buttonOffsetFromText, labelRect.y, (buttonWidth * 2) + buttonOffsetFromButton,
                buttonHeight);

            if (Widgets.ButtonText(highlightRect, curXenogerm != null ? curXenogerm.LabelCap : "-"))
            {
                Find.WindowStack.Add(new Dialog_SelectXenogermForSleeve(curSleeve, sleeveGrower.Map, curXenogerm, delegate (Xenogerm x)
                {
                    curXenogerm = x;
                    GeneUtility.ImplantXenogermItem(curSleeve, curXenogerm);
                    RecheckBodyOptions();
                    InitializeIndexes();
                }));
            }

            DrawExplanation(ref firstColumnPos, highlightRect.width + labelWidth + 15, 32, "AC.XenogermExplanation".Translate());
            if (ModCompatibility.AlienRacesIsActive)
            {
                var permittedRaces = GetPermittedRaces();
                DoSelectionButtons(ref firstColumnPos, "AC.SelectRace".Translate(), ref raceTypeIndex,
                    (ThingDef x) => x.LabelCap, permittedRaces, delegate (ThingDef x)
                    {
                        currentPawnKindDef.race = x;
                        CreateSleeve(curSleeve.gender);
                        raceTypeIndex = permittedRaces.IndexOf(x);
                    });
            }

            if (curXenogerm != null)
            {
                bool geneOptionsDrawn = false;
                foreach (var category in ACUtils.genesByCategories)
                {
                    var xenogermGenes = curXenogerm.GeneSet.genes.Where(x => x.exclusionTags.NullOrEmpty() is false
                    && x.exclusionTags.Contains(category.Key)).ToList();
                    if (xenogermGenes.Count > 1)
                    {
                        if (geneOptionsDrawn is false)
                        {
                            ListSeparator(ref firstColumnPos, LineSeparatorWidth, "AC.GeneOptions".Translate());
                            firstColumnPos.y += 5f;
                            geneOptionsDrawn = true;
                        }
                        var index = indexesPerCategory[category.Key];
                        DoSelectionButtons(ref firstColumnPos, category.Key.SplitCamelCase(), ref index,
                            (GeneDef x) => x.LabelCap, xenogermGenes, delegate (GeneDef x)
                            {
                                var gene = curSleeve.genes.GetGene(x);
                                curSleeve.genes.OverrideAllConflicting(gene);
                                UpdateSleeve();
                                indexesPerCategory[category.Key] = xenogermGenes.IndexOf(x);
                            });
                    }
                }
                if (ModCompatibility.HelixienAlteredCarbonIsActive)
                {
                    label = "AC.ConvertGenesToGermline".Translate();
                    var size = Text.CalcSize(label);
                    labelRect = GetLabelRect(label, ref firstColumnPos, size.x + 15);
                    Widgets.Label(labelRect, label);
                    bool oldValue = convertXenogenesToEndegones;
                    Widgets.Checkbox(new Vector2(labelRect.xMax, labelRect.y), ref convertXenogenesToEndegones);
                    if (oldValue != convertXenogenesToEndegones)
                    {

                    }
                }

                if (geneOptionsDrawn)
                {
                    DrawExplanation(ref firstColumnPos, highlightRect.width + labelWidth + 15, 32, "AC.GeneOptionsExplanation".Translate());
                }
            }
            ListSeparator(ref firstColumnPos, LineSeparatorWidth, "AC.BodyOptions".Translate());
            firstColumnPos.y += 5f;
            DoColorButtons(ref firstColumnPos, "AC.SkinColour".Translate(), GetPermittedSkinColors(), (KeyValuePair<GeneDef, Color> x) => x.Value,
                delegate (KeyValuePair<GeneDef, Color> selected)
                {
                    curSleeve.story.skinColorOverride = curSleeve.story.skinColorBase = null;
                    var gene = curSleeve.genes.GetGene(selected.Key);
                    if (selected.Key.endogeneCategory == EndogeneCategory.Melanin)
                    {
                        var melaninGene = curSleeve.genes.GenesListForReading
                            .FirstOrDefault(x => x.def.endogeneCategory == EndogeneCategory.Melanin);
                        if (melaninGene != null && gene != melaninGene)
                        {
                            curSleeve.genes.RemoveGene(melaninGene);
                        }
                    }

                    if (gene is null)
                    {
                        gene = curSleeve.genes.AddGene(selected.Key, selected.Key.endogeneCategory != EndogeneCategory.Melanin);
                    }
                    curSleeve.genes.OverrideAllConflicting(gene);
                    curSleeve.genes.Notify_GenesChanged(gene.def);
                    UpdateSleeve();
                });

            var permittedHeads = GetPermittedHeads();
            DoSelectionButtons(ref firstColumnPos, "AC.HeadShape".Translate(), ref headTypeIndex,
                (KeyValuePair<GeneDef, HeadTypeDef> x) => x.Value.defName, permittedHeads,
                delegate (KeyValuePair<GeneDef, HeadTypeDef> selected)
                {
                    var geneDef = selected.Key;
                    if (geneDef?.forcedHeadTypes.NullOrEmpty() is false)
                    {
                        var gene = curSleeve.genes.GetGene(selected.Key);
                        curSleeve.story.TryGetRandomHeadFromSet(geneDef.forcedHeadTypes);
                        curSleeve.genes.OverrideAllConflicting(gene);
                    }
                    else
                    {
                        curSleeve.story.headType = selected.Value;
                    }
                    UpdateSleeve();
                    headTypeIndex = permittedHeads.IndexOf(selected);
                });

            if (curSleeve.gender == Gender.Male)
            {
                var permittedBodyTypes = GetPermittedBodyTypes();
                DoSelectionButtons(ref firstColumnPos, "AC.BodyShape".Translate(), ref maleBodyTypeIndex,
                    (KeyValuePair<GeneDef, BodyTypeDef> x) => x.Value.defName, permittedBodyTypes, delegate (KeyValuePair<GeneDef, BodyTypeDef> x)
                    {
                        var gene = curSleeve.genes.GetGene(x.Key);
                        if (gene != null)
                        {
                            curSleeve.genes.OverrideAllConflicting(gene);
                        }
                        curSleeve.story.bodyType = x.Value;
                        UpdateSleeve();
                        maleBodyTypeIndex = permittedBodyTypes.IndexOf(x);
                    });
            }
            else if (curSleeve.gender == Gender.Female)
            {
                var permittedBodyTypes = GetPermittedBodyTypes();
                DoSelectionButtons(ref firstColumnPos, "AC.BodyShape".Translate(), ref femaleBodyTypeIndex,
                    (KeyValuePair<GeneDef, BodyTypeDef> x) => x.Value.defName, permittedBodyTypes, delegate (KeyValuePair<GeneDef, BodyTypeDef> x)
                    {
                        var gene = curSleeve.genes.GetGene(x.Key);
                        if (gene != null)
                        {
                            curSleeve.genes.OverrideAllConflicting(gene);
                        }
                        curSleeve.story.bodyType = x.Value;
                        UpdateSleeve();
                        femaleBodyTypeIndex = permittedBodyTypes.IndexOf(x);
                    });
            }
            var permittedHairs = GetPermittedHairs();
            if (!permittedHairs.NullOrEmpty())
            {
                DoColorButtons(ref firstColumnPos, "AC.HairColour".Translate(), GetPermittedHairColors(), (KeyValuePair<GeneDef, Color> x) => x.Value,
                    delegate (KeyValuePair<GeneDef, Color> selected)
                    {
                        var gene = curSleeve.genes.GetGene(selected.Key);
                        if (gene is null)
                        {
                            gene = curSleeve.genes.AddGene(selected.Key, false);
                        }

                        var hairGene = curSleeve.genes.GenesListForReading
                            .FirstOrDefault(x => selected.Key.endogeneCategory == x.def.endogeneCategory);
                        if (hairGene != null && gene != hairGene)
                        {
                            curSleeve.genes.RemoveGene(hairGene);
                        }

                        curSleeve.genes.OverrideAllConflicting(gene);
                        curSleeve.genes.Notify_GenesChanged(gene.def);
                        UpdateSleeve();
                    });

                DoSelectionButtons(ref firstColumnPos, "AC.HairType".Translate(), ref hairTypeIndex,
                (HairDef x) => x.LabelCap, permittedHairs, delegate (HairDef x)
                {
                    curSleeve.story.hairDef = x;
                    UpdateSleeve();
                    hairTypeIndex = permittedHairs.IndexOf(x);
                });
            }
            var permittedBeards = GetPermittedBeards();
            DoSelectionButtons(ref firstColumnPos, "AC.BeardType".Translate(), ref beardTypeIndex,
                (BeardDef x) => x.LabelCap, permittedBeards, delegate (BeardDef x)
                {
                    curSleeve.style.beardDef = x;
                    UpdateSleeve();
                    beardTypeIndex = permittedBeards.IndexOf(x);
                });

            DoSelectionButtons(ref firstColumnPos, "AC.SleeveQuality".Translate(), ref sleeveQualityIndex,
                (HediffDef x) => GetQualityLabel(sleeveQualities.IndexOf(x)), sleeveQualities, delegate (HediffDef x)
                {
                    sleeveQualityIndex = sleeveQualities.IndexOf(x);
                    ApplyHediffs();
                    UpdateGrowCost();
                });

            DrawExplanation(ref firstColumnPos, highlightRect.width + labelWidth + 15, 32, "AC.BodyOptionsExplanation".Translate());

            //Pawn Box
            Rect pawnBox = new Rect(secondColumnPos.x, secondColumnPos.y, 200, 200);
            Widgets.DrawMenuSection(pawnBox);
            Widgets.DrawShadowAround(pawnBox);
            Rect pawnBoxPawn = new Rect(pawnBox.x + pawnSpacingFromEdge, pawnBox.y + pawnSpacingFromEdge, pawnBox.width - (pawnSpacingFromEdge * 2), pawnBox.height - (pawnSpacingFromEdge * 2));
            UpdateSleeveGraphic();
            GUI.DrawTexture(pawnBoxPawn, PortraitsCache.Get(curSleeve, pawnBoxPawn.size, Rot4.South, default, 1f));
            Widgets.InfoCardButton(pawnBox.x + pawnBox.width - Widgets.InfoCardButtonSize - 10f, pawnBox.y + pawnBox.height - Widgets.InfoCardButtonSize - 10f, curSleeve);

            secondColumnPos.y = pawnBox.yMax + 15;
            //Hediff printout
            var heDiffListing = HealthCardUtility.VisibleHediffGroupsInOrder(curSleeve, false);
            List<Hediff> diffs = heDiffListing.SelectMany(group => group).ToList();
            Rect healthBoxLabel = GetLabelRect(ref secondColumnPos, 150, 32);
            Text.Font = GameFont.Medium;
            Widgets.Label(healthBoxLabel, "AC.SleeveHealthPreview".Translate().CapitalizeFirst());
            Rect healthBox = new Rect(pawnBox.x - 15, healthBoxLabel.yMax, pawnBox.width + 30, 50f);
            Widgets.DrawHighlight(healthBox);
            GUI.color = HealthUtility.GoodConditionColor;
            Listing_Standard diffListing = new Listing_Standard();
            Rect heDiffPrintout = healthBox.BottomPart(0.95f).LeftPart(0.95f).RightPart(0.95f);
            diffListing.Begin(heDiffPrintout);
            Text.Anchor = TextAnchor.MiddleLeft;
            for (int ii = 0; ii < diffs.Count; ++ii)
            {
                diffListing.Label(diffs[ii].LabelCap);
            }
            diffListing.End();
            healthBox.height = Mathf.Max(50f, diffs.Count * 25f);

            GUI.color = Color.white;
            if (Mouse.IsOver(healthBox))
            {
                Widgets.DrawHighlight(healthBox);
                TooltipHandler.TipRegion(healthBox, new TipSignal(() => GetHediffToolTip(diffs, curSleeve), 1147682));
            }

            secondColumnPos.y = healthBox.yMax + 15;

            Text.Font = GameFont.Small;
            var genes = curSleeve.genes?.GenesListForReading;
            if (genes == null || genes.Count == 0)
            {
                Color color = GUI.color;
                GUI.color = Color.gray;
                Rect rect13 = new Rect(secondColumnPos.x - 15, secondColumnPos.y, pawnBox.width + 30, 24f);
                if (Mouse.IsOver(rect13))
                {
                    Widgets.DrawHighlight(rect13);
                }
                Widgets.Label(rect13, "None".Translate());
                TooltipHandler.TipRegionByKey(rect13, "None");
                GUI.color = color;
            }
            else
            {
                Rect geneBox = default;
                var endogenes = genes.Where(x => curSleeve.genes.Endogenes.Contains(x)).ToList();
                Rect rect = default;
                if (endogenes.Any())
                {
                    label = "Endogenes".Translate().CapitalizeFirst();
                    secondColumnPos.x -= 15;
                    ListSeparator(ref secondColumnPos, healthBox.width, label);
                    secondColumnPos.x += 15;
                    secondColumnPos.y += 5;

                    geneBox = new Rect(secondColumnPos.x - 15, secondColumnPos.y, pawnBox.width + 50, 60);
                    rect = GenUI.DrawElementStack(geneBox, 22f, endogenes, delegate (Rect r, Gene gene)
                    {
                        DrawGene(r, gene, secondColumnPos);
                    }, (Gene gene) => Text.CalcSize(gene.LabelCap).x + 10f + 22);
                    secondColumnPos.y += rect.height;
                }

                var xenogenes = genes.Where(x => curSleeve.genes.Xenogenes.Contains(x)).ToList();
                if (xenogenes.Any())
                {
                    label = "Xenogenes".Translate().CapitalizeFirst();
                    secondColumnPos.x -= 15;
                    ListSeparator(ref secondColumnPos, healthBox.width, label);
                    secondColumnPos.x += 15;

                    secondColumnPos.y += 5;
                    geneBox.y = secondColumnPos.y;
                    rect = GenUI.DrawElementStack(geneBox, 22f, xenogenes, delegate (Rect r, Gene gene)
                    {
                        DrawGene(r, gene, secondColumnPos);
                    }, (Gene gene) => Text.CalcSize(gene.LabelCap).x + 10f + 22);
                    secondColumnPos.y += rect.height;
                }
            }

            if (ModCompatibility.RimJobWorldIsActive && ModCompatibility.HelixienAlteredCarbonIsActive)
            {
                Rect setBodyParts = new Rect(healthBox.x, secondColumnPos.y, healthBox.width, buttonHeight);
                if (ButtonTextSubtleCentered(setBodyParts, "AC.SetBodyParts".Translate().CapitalizeFirst()))
                {
                    Find.WindowStack.Add(new Window_BodyPartPicker(curSleeve, this));
                }
            }

            Widgets.EndScrollView();
            scrollHeightCount = (int)Mathf.Max(firstColumnPos.y, secondColumnPos.y);

            firstColumnPos.x = leftOffset;
            firstColumnPos.y = (inRect.y + inRect.height) - 170;
            Rect timeToGrowRect = GetLabelRect(ref firstColumnPos, 300, 32);
            Text.Font = GameFont.Medium;
            Widgets.Label(timeToGrowRect, "AC.TimeToGrow".Translate(GenDate.ToStringTicksToDays(ticksToGrow)));
            Text.Font = GameFont.Small;
            Rect growCostRect = GetLabelRect(ref firstColumnPos, inRect.width, 32);
            Widgets.Label(growCostRect, "  " + "AC.GrowCost".Translate(growCost));
            Widgets.DrawHighlight(new Rect(growCostRect.x, growCostRect.y, inRect.width - 50, growCostRect.height));

            DrawExplanation(ref firstColumnPos, inRect.width - 50, 50, "AC.SleeveCustomizationExplanation".Translate());

            Rect saveTemplateRect = new Rect(inRect.x, inRect.y + inRect.height - 32, 180, 32);
            if (Widgets.ButtonText(saveTemplateRect, "AC.SaveTemplate".Translate()))
            {
                Find.WindowStack.Add(new Dialog_PresetList_Save(this));
            }
            Rect loadTemplateRect = new Rect(saveTemplateRect.xMax + 15, saveTemplateRect.y, saveTemplateRect.width, saveTemplateRect.height);
            if (Widgets.ButtonText(loadTemplateRect, "AC.LoadTemplate".Translate()))
            {
                Find.WindowStack.Add(new Dialog_PresetList_Load(this));
            }

            Rect acceptRect = new Rect(inRect.xMax - (loadTemplateRect.width * 2f) - 15, loadTemplateRect.y, loadTemplateRect.width, loadTemplateRect.height);
            if (Widgets.ButtonText(acceptRect, "Accept".Translate().CapitalizeFirst()))
            {
                sleeveGrower.StartGrowth(curSleeve, ticksToGrow, growCost);
                Close();
            }

            Rect cancelRect = new Rect(acceptRect.xMax + 15, acceptRect.y, acceptRect.width, acceptRect.height);
            if (Widgets.ButtonText(cancelRect, "AC.Cancel".Translate().CapitalizeFirst()))
            {
                Close();
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }
        private void DrawGene(Rect r, Gene gene, Vector2 pos)
        {
            Color color2 = GUI.color;
            GUI.color = StackElementBackground;
            GUI.DrawTexture(r, BaseContent.WhiteTex);
            GUI.color = color2;

            if (Mouse.IsOver(r))
            {
                Widgets.DrawHighlight(r);
            }

            var iconRect = new Rect(r.x, r.y, 22f, 22f);
            Color iconColor = gene.def.IconColor;
            Widgets.DefIcon(iconRect, gene.def, null, 1f, null, drawPlaceholder: false, iconColor);

            var labelRect = new Rect(iconRect.xMax, r.y, r.width - 10f - 15f, r.height);
            Widgets.Label(labelRect, gene.LabelCap);
            if (Mouse.IsOver(r))
            {
                Gene trLocal = gene;
                TooltipHandler.TipRegion(tip: new TipSignal(() => trLocal.def.DescriptionFull, (int)pos.y * 37), rect: r);
            }
        }
        public string GetQualityLabel(int sleeveQualityIndex)
        {
            return ((QualityCategory)sleeveQualityIndex).GetLabel().CapitalizeFirst();
        }

        public void UpdateGrowCost()
        {
            ticksToGrow = AlteredCarbonMod.settings.baseGrowingTimeDuration;
            ticksToGrow += sleeveQualitiesTimeCost[sleeveQualities[sleeveQualityIndex]];
            ticksToGrow = Mathf.Max(AlteredCarbonMod.settings.baseGrowingTimeDuration, ticksToGrow);
            growCost = 12 * (ticksToGrow / GenDate.TicksPerDay);
        }

        public void ApplyHediffs()
        {
            if (qualityDiff != null)
            {
                Hediff hediff = curSleeve.health.hediffSet.GetFirstHediffOfDef(qualityDiff);
                if (hediff != null)
                {
                    curSleeve.health.RemoveHediff(hediff);
                }
            }
            qualityDiff = sleeveQualities[sleeveQualityIndex];
            curSleeve.health.AddHediff(qualityDiff, null);
        }

        public static string GetHediffToolTip(IEnumerable<Hediff> diffs, Pawn pawn)
        {
            string str = "";
            foreach (Hediff hediff in diffs)
            {
                str += hediff.GetTooltip(pawn, false) + "\n";
            }
            return str;
        }

        public void UpdateSleeve()
        {
            RecheckBodyOptions();
            InitializeIndexes();
        }

        public void UpdateSleeveGraphic()
        {
            curSleeve.Drawer.renderer.graphics.ResolveAllGraphics();
            PortraitsCache.SetDirty(curSleeve);
            PortraitsCache.PortraitsCacheUpdate();
            GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(curSleeve);
        }

        private void RecheckBodyOptions()
        {
            var permittedHeads = GetPermittedHeads(true).Select(x => x.Value).ToList();
            var permittedBodyTypes = GetPermittedBodyTypes(true).Select(x => x.Value).ToList();
            var permittedHairs = GetPermittedHairs();
            var permittedBeards = GetPermittedBeards();
            if (permittedHeads.Contains(curSleeve.story.headType) is false)
            {
                curSleeve.story.headType = permittedHeads.RandomElement();
            }

            if (permittedBodyTypes.Contains(curSleeve.story.bodyType) is false)
            {
                curSleeve.story.bodyType = permittedBodyTypes.RandomElement();
            }
            if (permittedHairs.Contains(curSleeve.story.hairDef) is false)
            {
                curSleeve.story.hairDef = permittedHairs.RandomElement();
            }
            if (permittedBeards.Contains(curSleeve.style.beardDef) is false)
            {
                curSleeve.style.beardDef = permittedBeards.RandomElement();
            }
        }

        public void RemoveAllTraits(Pawn pawn)
        {
            if (pawn.story != null)
            {
                pawn.story.traits = new TraitSet(pawn);
            }
        }

        public void RemoveAllHediffs(Pawn pawn)
        {
            pawn.health = new Pawn_HealthTracker(pawn);
        }

        public string ExtractHeadLabels(string path)
        {
            string str = Regex.Replace(path, ".*/[A-Z]+?_", "", RegexOptions.IgnoreCase).Replace("_", " ");
            return str;
        }

        private List<ThingDef> GetPermittedRaces()
        {
            List<ThingDef> excludedRaces = new List<ThingDef>();
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs.Where(def => def.category == ThingCategory.Pawn))
            {
                if (def.GetModExtension<ExcludeRacesModExtension>() is ExcludeRacesModExtension props)
                {
                    if (!props.canBeGrown)
                    {
                        excludedRaces.Add(def);
                    }
                }
            }
            return ModCompatibility.GetGrowableRaces(excludedRaces).OrderBy(entry => entry.LabelCap.RawText).ToList();
        }


        private static List<BodyTypeDef> invalidBodies = new List<BodyTypeDef>
        {
            BodyTypeDefOf.Baby, BodyTypeDefOf.Child
        };
        public List<KeyValuePair<GeneDef, BodyTypeDef>> GetPermittedBodyTypes(bool geneActiveCheck = false)
        {
            var keyValuePairs = new List<KeyValuePair<GeneDef, BodyTypeDef>>();
            foreach (var gene in curSleeve.genes.GenesListForReading)
            {
                if ((!geneActiveCheck || gene.Active) && gene.def.bodyType != null)
                {
                    keyValuePairs.Add(new KeyValuePair<GeneDef, BodyTypeDef>(gene.def, gene.def.bodyType.Value.ToBodyType(curSleeve)));
                }
            }
            if (keyValuePairs.Any())
            {
                return keyValuePairs;
            }

            var list = (ModCompatibility.AlienRacesIsActive ?
                ModCompatibility.GetAllowedBodyTypes(curSleeve.def) :
                DefDatabase<BodyTypeDef>.AllDefsListForReading).Except(invalidBodies).ToList();
            if (curSleeve.gender == Gender.Male)
            {
                list = list.Where(x => x != BodyTypeDefOf.Female).ToList();
            }
            else if (curSleeve.gender == Gender.Female)
            {
                list = list.Where(x => x != BodyTypeDefOf.Male).ToList();
            }
            foreach (var entry in list)
            {
                keyValuePairs.Add(new KeyValuePair<GeneDef, BodyTypeDef>(null, entry));
            }
            return keyValuePairs;
        }

        private List<BeardDef> GetPermittedBeards()
        {
            return DefDatabase<BeardDef>.AllDefs.Where(x => PawnStyleItemChooser.WantsToUseStyle(curSleeve, x)).ToList();
        }

        private List<HairDef> GetPermittedHairs()
        {
            return (ModCompatibility.AlienRacesIsActive
                ? ModCompatibility.GetPermittedHair(currentPawnKindDef.race)
                : DefDatabase<HairDef>.AllDefs.ToList()).Where(x => PawnStyleItemChooser.WantsToUseStyle(curSleeve, x)).ToList();
        }

        private static List<HeadTypeDef> invalidHeads = new List<HeadTypeDef>
        {
            HeadTypeDefOf.Skull, HeadTypeDefOf.Stump
        };
        public List<KeyValuePair<GeneDef, HeadTypeDef>> GetPermittedHeads(bool geneActiveCheck = false)
        {
            var keyValuePairs = new List<KeyValuePair<GeneDef, HeadTypeDef>>();
            foreach (var gene in curSleeve.genes.GenesListForReading)
            {
                if ((!geneActiveCheck || gene.Active) && gene.def.forcedHeadTypes.NullOrEmpty() is false)
                {
                    foreach (var head in gene.def.forcedHeadTypes)
                    {
                        keyValuePairs.Add(new KeyValuePair<GeneDef, HeadTypeDef>(gene.def, head));
                    }
                }
            }

            if (keyValuePairs.Any())
            {
                return keyValuePairs;
            }
            var list = DefDatabase<HeadTypeDef>.AllDefs.Except(invalidHeads).Where(x => CanUseHeadType(x)).ToList();
            foreach (var entry in list)
            {
                keyValuePairs.Add(new KeyValuePair<GeneDef, HeadTypeDef>(null, entry));
            }
            return keyValuePairs;
            bool CanUseHeadType(HeadTypeDef head)
            {
                if (!head.requiredGenes.NullOrEmpty())
                {
                    if (curSleeve.genes == null)
                    {
                        return false;
                    }
                    foreach (GeneDef requiredGene in head.requiredGenes)
                    {
                        if (!curSleeve.genes.HasGene(requiredGene))
                        {
                            return false;
                        }
                    }
                }
                return head.gender == 0 || head.gender == curSleeve.gender;
            }
        }

        public List<KeyValuePair<GeneDef, Color>> GetPermittedSkinColors(bool geneActiveCheck = false)
        {
            var skinColors = new Dictionary<GeneDef, Color>();
            foreach (var geneDef in DefDatabase<GeneDef>.AllDefsListForReading)
            {
                if (geneDef.skinColorBase != null && geneDef.endogeneCategory == EndogeneCategory.Melanin)
                {
                    skinColors[geneDef] = geneDef.skinColorBase.Value;
                }
            }
            foreach (var gene in curSleeve.genes.GenesListForReading)
            {
                if ((!geneActiveCheck || gene.Active))
                {
                    if (gene.def.skinColorBase != null && gene.def.endogeneCategory == EndogeneCategory.Melanin)
                    {
                        skinColors[gene.def] = gene.def.skinColorBase.Value;
                    }
                    else if (gene.def.skinColorOverride != null)
                    {
                        skinColors[gene.def] = gene.def.skinColorOverride.Value;
                    }
                }
            }
            return skinColors.ToList();
        }
        public List<KeyValuePair<GeneDef, Color>> GetPermittedHairColors(bool geneActiveCheck = false)
        {
            var hairColors = new Dictionary<GeneDef, Color>();
            foreach (var geneDef in DefDatabase<GeneDef>.AllDefsListForReading)
            {
                if (geneDef.hairColorOverride != null)
                {
                    hairColors[geneDef] = geneDef.hairColorOverride.Value;
                }
            }
            foreach (var gene in curSleeve.genes.GenesListForReading)
            {
                if ((!geneActiveCheck || gene.Active) && gene.def.hairColorOverride != null && gene.def.endogeneCategory == EndogeneCategory.HairColor)
                {
                    hairColors[gene.def] = gene.def.hairColorOverride.Value;
                }
            }
            return hairColors.ToList();
        }

        private void CopyBodyFrom(Pawn source)
        {
            curSleeve.gender = source.gender;
            curSleeve.kindDef = source.kindDef;
            curSleeve.story.bodyType = source.story.bodyType;
            curSleeve.story.hairDef = source.story.hairDef;
            curSleeve.style.beardDef = source.style.beardDef;
            if (ModCompatibility.AlienRacesIsActive)
            {
                ModCompatibility.SetSkinColorFirst(curSleeve, ModCompatibility.GetSkinColorFirst(source));
                ModCompatibility.SetSkinColorSecond(curSleeve, ModCompatibility.GetSkinColorSecond(source));

                ModCompatibility.SetHairColorFirst(curSleeve, ModCompatibility.GetHairColorFirst(source));
                ModCompatibility.SetHairColorSecond(curSleeve, ModCompatibility.GetHairColorSecond(source));

                ModCompatibility.CopyBodyAddons(source, curSleeve);
            }
            else
            {

                curSleeve.story.HairColor = source.story.HairColor;
                curSleeve.story.SkinColorBase = source.story.SkinColor;
                curSleeve.story.headType = source.story.headType;
            }
            UpdateSleeve();
        }

        public void LoadSleeve(SleevePreset preset)
        {
            curSleeve = preset.sleeve;
            currentPawnKindDef = curSleeve.kindDef;
            InitializeIndexes();
        }

        private void InitializeIndexes()
        {
            hairTypeIndex = GetPermittedHairs().IndexOf(curSleeve.story.hairDef);
            beardTypeIndex = GetPermittedBeards().IndexOf(curSleeve.style.beardDef);
            headTypeIndex = GetPermittedHeads().Select(x => x.Value).ToList().IndexOf(curSleeve.story.headType);
            if (curXenogerm != null)
            {
                indexesPerCategory = new Dictionary<string, int>();
                var genes = curXenogerm.GeneSet.genes.Where(x => x.exclusionTags.NullOrEmpty() is false);
                foreach (var gene in genes)
                {
                    foreach (var tag in gene.exclusionTags)
                    {
                        var genesOfThisTag = curSleeve.genes.GenesListForReading.Where(x => x.def.exclusionTags.NullOrEmpty() is false
                            && x.def.exclusionTags.Contains(tag)).ToList();
                        var activeGene = genesOfThisTag.FirstOrDefault(x => x.Active);
                        indexesPerCategory[tag] = genesOfThisTag.IndexOf(activeGene);
                    }
                }
            }

            if (ModCompatibility.AlienRacesIsActive)
            {
                raceTypeIndex = GetPermittedRaces().IndexOf(curSleeve.def);
            }

            if (curSleeve.gender == Gender.Male)
            {
                maleBodyTypeIndex = GetPermittedBodyTypes().Select(x => x.Value).ToList().IndexOf(curSleeve.story.bodyType);
            }
            else if (curSleeve.gender == Gender.Female)
            {
                femaleBodyTypeIndex = GetPermittedBodyTypes().Select(x => x.Value).ToList().IndexOf(curSleeve.story.bodyType);
            }

            foreach (HediffDef hediff in sleeveQualities)
            {
                if (curSleeve.health.hediffSet.HasHediff(hediff))
                {
                    sleeveQualityIndex = sleeveQualities.IndexOf(hediff);
                }
            }
        }
        private void CreateSleeve(Gender gender)
        {
            if (ModCompatibility.AlienRacesIsActive)
            {
                ModCompatibility.UpdateGenderRestrictions(currentPawnKindDef.race, out allowMales, out allowFemales);
                if (gender == Gender.Male && !allowMales)
                {
                    gender = Gender.Female;
                }
                if (gender == Gender.Female && !allowFemales)
                {
                    gender = Gender.Male;
                }
            }
            curSleeve = PawnGenerator.GeneratePawn(new PawnGenerationRequest(currentPawnKindDef, null, PawnGenerationContext.NonPlayer,
                -1, true, false, false, false, false, 0f, false, true, true, false, false, false, true,
                fixedGender: gender, fixedBiologicalAge: 20, fixedChronologicalAge: 20));
            curSleeve.story.Adulthood = null;
            curSleeve.Name = new NameSingle("AC.EmptySleeve".Translate());
            curSleeve.story.title = "";
            curSleeve?.equipment.DestroyAllEquipment();
            curSleeve?.inventory.DestroyAll();
            curSleeve.apparel.DestroyAll();
            RemoveAllTraits(curSleeve);
            if (curSleeve.playerSettings == null)
            {
                curSleeve.playerSettings = new Pawn_PlayerSettings(curSleeve);
            }

            curSleeve.playerSettings.medCare = MedicalCareCategory.Best;
            curSleeve.skills = new Pawn_SkillTracker(curSleeve);
            curSleeve.needs = new Pawn_NeedsTracker(curSleeve);
            curSleeve.workSettings = new Pawn_WorkSettings(curSleeve);
            curSleeve.needs.mood.thoughts = new ThoughtHandler(curSleeve);
            curSleeve.timetable = new Pawn_TimetableTracker(curSleeve);

            if (curSleeve.needs?.mood?.thoughts?.memories?.Memories != null)
            {
                for (int num = curSleeve.needs.mood.thoughts.memories.Memories.Count - 1; num >= 0; num--)
                {
                    curSleeve.needs.mood.thoughts.memories.RemoveMemory(curSleeve.needs.mood.thoughts.memories.Memories[num]);
                }
            }
            RemoveAllHediffs(curSleeve);

            if (curSleeve.workSettings != null)
            {
                curSleeve.workSettings.EnableAndInitialize();
            }

            if (curSleeve.skills != null)
            {
                curSleeve.skills.Notify_SkillDisablesChanged();
            }
            if (!curSleeve.Dead && curSleeve.RaceProps.Humanlike)
            {
                curSleeve.needs.mood.thoughts.situational.Notify_SituationalThoughtsDirty();
            }

            curSleeve.story.Childhood = AC_DefOf.VFEU_VatGrownChild;
            curSleeve.story.Childhood = AC_DefOf.VFEU_VatGrownAdult;
            curSleeve.ideo = new Pawn_IdeoTracker(curSleeve);
            curSleeve.story.favoriteColor = null;
            if (ModsConfig.IdeologyActive)
            {
                curSleeve.style.BodyTattoo = TattooDefOf.NoTattoo_Body;
                curSleeve.style.FaceTattoo = TattooDefOf.NoTattoo_Face;
            }


            ApplyHediffs();
            UpdateGrowCost();
            UpdateSleeve();
        }
    }
}
