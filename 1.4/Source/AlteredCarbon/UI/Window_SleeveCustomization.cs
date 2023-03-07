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
        private GeneDef geneQuality;

        public static Dictionary<GeneDef, int> sleeveQualitiesTimeCost = new Dictionary<GeneDef, int>
        {
            {AC_DefOf.VFEU_SleeveQuality_Awful, 0 },
            {AC_DefOf.VFEU_SleeveQuality_Poor, GenDate.TicksPerDay * 2 },
            {AC_DefOf.VFEU_SleeveQuality_Normal, GenDate.TicksPerDay * 3 },
            {AC_DefOf.VFEU_SleeveQuality_Good, GenDate.TicksPerDay * 5 },
            {AC_DefOf.VFEU_SleeveQuality_Excellent, GenDate.TicksPerDay * 10 },
            {AC_DefOf.VFEU_SleeveQuality_Masterwork, GenDate.TicksPerDay * 15 },
            {AC_DefOf.VFEU_SleeveQuality_Legendary, GenDate.TicksPerDay * 30 },
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

        public Window_SleeveCustomization(Building_SleeveGrower sleeveGrower, Pawn pawnToClone)
        {
            this.sleeveGrower = sleeveGrower;
            Init(pawnToClone.kindDef, pawnToClone.gender);
            ACUtils.CopyBody(pawnToClone, curSleeve, copyGenesPartially: true);
            if (pawnToClone.genes.GenesListForReading.Any(x => ACUtils.sleeveQualities.Contains(x.def)) is false)
            {
                ApplyGeneQuality();
            }
            var xenogenes = pawnToClone.genes.Xenogenes.Select(x => x.def).ToList();
            if (xenogenes.Any() )
            {
                curXenogerm = TryFindXenogerm(xenogenes);
                if (curXenogerm is null)
                {
                    Messages.Message("AC.PawnBodyCouldntBeFullyCopied".Translate(pawnToClone.Named("PAWN"), string.Join(", ", xenogenes.Select(x => x.label))), MessageTypeDefOf.CautionInput);
                }
            }
            RecheckEverything();
            InitUI();
        }

        private void InitUI()
        {
            forcePause = true;
            absorbInputAroundWindow = true;
        }

        private void Init(PawnKindDef pawnKindDef, Gender? gender = null)
        {
            currentPawnKindDef = pawnKindDef;
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

            float innerRectYOffset = 50;
            Vector2 firstColumnPos = new Vector2(leftOffset, innerRectYOffset);
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
                    if (curSleeve.genes.CustomXenotype != null)
                    {
                        curSleeve.genes.CustomXenotype.inheritable = true;
                    }
                    convertedGenes = new List<Gene>();
                    RecheckBodyOptions();
                    InitializeIndexes();
                }));
            }

            DrawExplanation(ref firstColumnPos, highlightRect.width + labelWidth + 15, 32, "AC.XenogermExplanation".Translate());
            if (ModCompatibility.AlienRacesIsActive)
            {
                var permittedRaces = ModCompatibility.GetPermittedRaces();
                DoSelectionButtons(ref firstColumnPos, "AC.SelectRace".Translate(), ref raceTypeIndex,
                    (ThingDef x) => x.LabelCap, permittedRaces, delegate (ThingDef x)
                    {
                        var oldRace = currentPawnKindDef.race;
                        currentPawnKindDef.race = x;
                        CreateSleeve(curSleeve.gender);
                        raceTypeIndex = permittedRaces.IndexOf(x);
                        currentPawnKindDef.race = oldRace;
                    }, floatMenu: false);
            }

            var genes = curSleeve.genes?.GenesListForReading;
            bool geneOptionsDrawn = false;
            foreach (var category in ACUtils.genesByCategories)
            {
                var groupedGenes = genes.Where(x => x.def.exclusionTags.NullOrEmpty() is false
                && x.def.exclusionTags.Contains(category.Key)).Select(x => x.def).ToList();
                if (groupedGenes.Count > 1)
                {
                    if (geneOptionsDrawn is false)
                    {
                        ListSeparator(ref firstColumnPos, LineSeparatorWidth, "AC.GeneOptions".Translate());
                        firstColumnPos.y += 5f;
                        geneOptionsDrawn = true;
                    }
                    var index = indexesPerCategory[category.Key];
                    DoSelectionButtons(ref firstColumnPos, category.Key.SplitCamelCase().FirstCharToUpper(), ref index,
                        (GeneDef x) => x.LabelCap, groupedGenes, delegate (GeneDef x)
                        {
                            GeneUtils.ApplyGene(x, curSleeve, curSleeve.IsXenogene(x));
                            RecheckEverything();
                            indexesPerCategory[category.Key] = groupedGenes.IndexOf(x);
                        }, floatMenu: false);
                }
            }
            if (curXenogerm != null)
            {
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
                        if (convertXenogenesToEndegones)
                        {
                            convertedGenes = genes.Where(x => curSleeve.genes.Xenogenes.Contains(x) 
                                && x.def.displayCategory != GeneCategoryDefOf.Archite).ToList();
                            curSleeve.genes.endogenes.AddRange(convertedGenes);
                            curSleeve.genes.xenogenes.RemoveAll(x => convertedGenes.Contains(x));
                        }
                        else
                        {
                            curSleeve.genes.endogenes.RemoveAll(x => convertedGenes.Contains(x));
                            curSleeve.genes.xenogenes.AddRange(convertedGenes);
                        }
                        RecheckEverything();
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
                    if (ModCompatibility.AlienRacesIsActive)
                    {
                        ModCompatibility.SetSkinColorFirst(curSleeve, selected.Value);
                    }

                    curSleeve.story.skinColorOverride = curSleeve.story.skinColorBase = null;
                    var gene = GeneUtils.ApplyGene(selected.Key, curSleeve, curSleeve.IsXenogene(selected.Key));
                    if (gene != null)
                    {
                        if (selected.Key.endogeneCategory == EndogeneCategory.Melanin)
                        {
                            var melaninGene = curSleeve.genes.GenesListForReading
                                .FirstOrDefault(x => x.def.endogeneCategory == EndogeneCategory.Melanin);
                            if (melaninGene != null && gene != melaninGene)
                            {
                                curSleeve.genes.RemoveGene(melaninGene);
                            }
                        }
                        foreach (var otherGene in curSleeve.genes.GenesListForReading)
                        {
                            if (otherGene != gene && (otherGene.def.skinColorOverride != null || otherGene.def.skinColorBase != null))
                            {
                                otherGene.OverrideBy(gene);
                            }
                        }
                    }
                    RecheckEverything();
                });

            var permittedHeads = GetPermittedHeads();
            DoSelectionButtons(ref firstColumnPos, "AC.HeadShape".Translate(), ref headTypeIndex,
                (KeyValuePair<GeneDef, HeadTypeDef> x) => ExtractHeadLabels(x.Value.defName), permittedHeads,
                delegate (KeyValuePair<GeneDef, HeadTypeDef> selected)
                {
                    if (selected.Key != null)
                    {
                        GeneUtils.ApplyGene(selected.Key, curSleeve, curSleeve.IsXenogene(selected.Key));
                    }
                    else
                    {
                        curSleeve.story.headType = selected.Value;
                    }
                    RecheckEverything();
                    headTypeIndex = permittedHeads.IndexOf(selected);
                }, floatMenu: true);

            if (curSleeve.gender == Gender.Male)
            {
                var permittedBodyTypes = GetPermittedBodyTypes();
                DoSelectionButtons(ref firstColumnPos, "AC.BodyShape".Translate(), ref maleBodyTypeIndex,
                    (KeyValuePair<GeneDef, BodyTypeDef> x) => x.Value.defName, permittedBodyTypes, delegate (KeyValuePair<GeneDef, BodyTypeDef> x)
                    {
                        if (x.Key != null)
                        {
                            GeneUtils.ApplyGene(x.Key, curSleeve, curSleeve.IsXenogene(x.Key));
                        }
                        else
                        {
                            curSleeve.story.bodyType = x.Value;
                        }
                        RecheckEverything();
                        maleBodyTypeIndex = permittedBodyTypes.IndexOf(x);
                    }, floatMenu: true);
            }
            else if (curSleeve.gender == Gender.Female)
            {
                var permittedBodyTypes = GetPermittedBodyTypes();
                DoSelectionButtons(ref firstColumnPos, "AC.BodyShape".Translate(), ref femaleBodyTypeIndex,
                    (KeyValuePair<GeneDef, BodyTypeDef> x) => x.Value.defName, permittedBodyTypes, delegate (KeyValuePair<GeneDef, BodyTypeDef> x)
                    {
                        if (x.Key != null)
                        {
                            GeneUtils.ApplyGene(x.Key, curSleeve, curSleeve.IsXenogene(x.Key));
                        }
                        else
                        {
                            curSleeve.story.bodyType = x.Value;
                        }
                        RecheckEverything();
                        femaleBodyTypeIndex = permittedBodyTypes.IndexOf(x);
                    }, floatMenu: true);
            }
            var permittedHairs = GetPermittedHairs();
            if (!permittedHairs.NullOrEmpty())
            {
                DoColorButtons(ref firstColumnPos, "AC.HairColour".Translate(), GetPermittedHairColors(), (KeyValuePair<GeneDef, Color> x) => x.Value,
                    delegate (KeyValuePair<GeneDef, Color> selected)
                    {
                        if (ModCompatibility.AlienRacesIsActive)
                        {
                            ModCompatibility.SetHairColorFirst(curSleeve, selected.Value);
                        }
                        var gene = GeneUtils.ApplyGene(selected.Key, curSleeve, curSleeve.IsXenogene(selected.Key));
                        if (gene != null)
                        {
                            var hairGene = curSleeve.genes.GenesListForReading
    .FirstOrDefault(x => selected.Key.endogeneCategory == x.def.endogeneCategory);
                            if (hairGene != null && gene != hairGene)
                            {
                                curSleeve.genes.RemoveGene(hairGene);
                            }
                        }

                        RecheckEverything();
                    });

                DoSelectionButtons(ref firstColumnPos, "AC.HairType".Translate(), ref hairTypeIndex,
                (HairDef x) => x.LabelCap, permittedHairs, delegate (HairDef x)
                {
                    curSleeve.story.hairDef = x;
                    RecheckEverything();
                    hairTypeIndex = permittedHairs.IndexOf(x);
                }, floatMenu: false);
            }
            var permittedBeards = GetPermittedBeards();
            DoSelectionButtons(ref firstColumnPos, "AC.BeardType".Translate(), ref beardTypeIndex,
                (BeardDef x) => x.LabelCap, permittedBeards, delegate (BeardDef x)
                {
                    curSleeve.style.beardDef = x;
                    RecheckEverything();
                    beardTypeIndex = permittedBeards.IndexOf(x);
                }, floatMenu: false);

            DoSelectionButtons(ref firstColumnPos, "AC.SleeveQuality".Translate(), ref sleeveQualityIndex,
                (GeneDef x) => GetQualityLabel(ACUtils.sleeveQualities.IndexOf(x)), ACUtils.sleeveQualities, delegate (GeneDef x)
                {
                    sleeveQualityIndex = ACUtils.sleeveQualities.IndexOf(x);
                    ApplyGeneQuality();
                    UpdateGrowCost();
                }, floatMenu: true);

            DrawExplanation(ref firstColumnPos, highlightRect.width + labelWidth + 15, 32, "AC.BodyOptionsExplanation".Translate());

            //Pawn Box
            Rect pawnBox = new Rect(secondColumnPos.x, secondColumnPos.y, 200, 200);
            Widgets.DrawMenuSection(pawnBox);
            Widgets.DrawShadowAround(pawnBox);
            Rect pawnBoxPawn = new Rect(pawnBox.x + pawnSpacingFromEdge, pawnBox.y + pawnSpacingFromEdge, pawnBox.width - (pawnSpacingFromEdge * 2), pawnBox.height - (pawnSpacingFromEdge * 2));
            curSleeve.RefreshGraphic();
            GUI.DrawTexture(pawnBoxPawn, PortraitsCache.Get(curSleeve, pawnBoxPawn.size, curSleeve.Rotation, default, 1f));
            Widgets.InfoCardButton(pawnBox.x + pawnBox.width - Widgets.InfoCardButtonSize - 10f, pawnBox.y + pawnBox.height - 
                Widgets.InfoCardButtonSize - 10f, curSleeve);
            if (Widgets.ButtonImage(new Rect(pawnBox.x + pawnBox.width - Widgets.InfoCardButtonSize - 10f, pawnBox.y + 10, 24, 24), RotateSleeve))
            {
                curSleeve.Rotation = curSleeve.Rotation.Rotated(RotationDirection.Clockwise);
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            }

            if (Widgets.ButtonImage(new Rect(pawnBox.x + 10f, pawnBox.y + 10, 24, 24), RandomizeSleeve))
            {
                var oldRace = currentPawnKindDef.race;
                currentPawnKindDef.race = curSleeve.def;
                CreateSleeve(curSleeve.gender);
                currentPawnKindDef.race = oldRace;
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            }

            secondColumnPos.y = pawnBox.yMax + 15;

            //List<Hediff> hediffs = HealthCardUtility.VisibleHediffGroupsInOrder(curSleeve, false).SelectMany(group => group).ToList();
            //Rect healthBoxLabel = GetLabelRect(ref secondColumnPos, 150, 32);
            //Text.Font = GameFont.Medium;
            //Widgets.Label(healthBoxLabel, "AC.SleeveHealthPreview".Translate().CapitalizeFirst());
            //Rect healthBox = new Rect(pawnBox.x - 15, healthBoxLabel.yMax, pawnBox.width + 30, (hediffs.Count * 25f) + 10);
            //Widgets.DrawHighlight(healthBox);
            //GUI.color = HealthUtility.GoodConditionColor;
            //Listing_Standard diffListing = new Listing_Standard();
            //diffListing.Begin(healthBox.ContractedBy(5));
            //Text.Anchor = TextAnchor.MiddleLeft;
            //for (int ii = 0; ii < hediffs.Count; ++ii)
            //{
            //    diffListing.Label(hediffs[ii].LabelCap);
            //}
            //diffListing.End();
            //
            //GUI.color = Color.white;
            //if (Mouse.IsOver(healthBox))
            //{
            //    Widgets.DrawHighlight(healthBox);
            //    TooltipHandler.TipRegion(healthBox, new TipSignal(() => GetHediffToolTip(hediffs, curSleeve), 1147682));
            //}
            //secondColumnPos.y = healthBox.yMax + 15;

            Text.Font = GameFont.Small;
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
                Rect rect = default;

                var endogenes = genes.Where(x => curSleeve.genes.Endogenes.Contains(x)).ToList();
                GeneUtils.DrawGenes(ref secondColumnPos, "Endogenes".Translate().CapitalizeFirst(), pawnBox, pawnBox.width + 30, ref geneBox, endogenes, ref rect);
                var xenogenes = genes.Where(x => curSleeve.genes.Xenogenes.Contains(x)).ToList();
                GeneUtils.DrawGenes(ref secondColumnPos, "Xenogenes".Translate().CapitalizeFirst(), pawnBox, pawnBox.width + 30, ref geneBox, xenogenes, ref rect);
            }

            //if (ModCompatibility.RimJobWorldIsActive && ModCompatibility.HelixienAlteredCarbonIsActive)
            //{
            //    Rect setBodyParts = new Rect(healthBox.x, secondColumnPos.y, healthBox.width, buttonHeight);
            //    if (ButtonTextSubtleCentered(setBodyParts, "AC.SetBodyParts".Translate().CapitalizeFirst()))
            //    {
            //        Find.WindowStack.Add(new Window_BodyPartPicker(curSleeve, this));
            //    }
            //}

            Widgets.EndScrollView();
            scrollHeightCount = (int)(Mathf.Max(firstColumnPos.y, secondColumnPos.y) - innerRectYOffset);

            firstColumnPos.x = leftOffset;
            firstColumnPos.y = (inRect.y + inRect.height) - 170;
            Rect timeToGrowRect = GetLabelRect(ref firstColumnPos, 300, 32);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(timeToGrowRect, "AC.TimeToGrow".Translate(GenDate.ToStringTicksToDays(ticksToGrow)));
            Text.Font = GameFont.Small;
            Rect growCostRect = GetLabelRect(ref firstColumnPos, inRect.width, 32);
            Widgets.Label(growCostRect, "  " + "AC.GrowCost".Translate(growCost));
            Widgets.DrawHighlight(new Rect(growCostRect.x, growCostRect.y, inRect.width - 50, growCostRect.height));
            Text.Anchor = TextAnchor.UpperLeft;

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

            Rect cancelRect = new Rect(inRect.xMax - (loadTemplateRect.width * 2f) - 15, loadTemplateRect.y, loadTemplateRect.width, loadTemplateRect.height);
            if (Widgets.ButtonText(cancelRect, "AC.Cancel".Translate().CapitalizeFirst()))
            {
                Close();
            }

            Rect acceptRect = new Rect(cancelRect.xMax + 15, cancelRect.y, cancelRect.width, cancelRect.height);
            if (Widgets.ButtonText(acceptRect, "AC.StartGrowing".Translate().CapitalizeFirst()))
            {
                sleeveGrower.StartGrowth(curSleeve, curXenogerm, ticksToGrow, growCost);
                Close();
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        public string GetQualityLabel(int sleeveQualityIndex)
        {
            return ((QualityCategory)sleeveQualityIndex).GetLabel().CapitalizeFirst();
        }

        public void UpdateGrowCost()
        {
            ticksToGrow = AlteredCarbonMod.settings.baseGrowingTimeDuration;
            ticksToGrow += sleeveQualitiesTimeCost[ACUtils.sleeveQualities[sleeveQualityIndex]];
            ticksToGrow = Mathf.Max(AlteredCarbonMod.settings.baseGrowingTimeDuration, ticksToGrow);
            if (convertXenogenesToEndegones)
            {
                ticksToGrow *= 2;
            }
            growCost = 12 * (ticksToGrow / GenDate.TicksPerDay);
        }

        public void ApplyGeneQuality()
        {
            foreach (var geneQuality in ACUtils.sleeveQualities)
            {
                var gene = curSleeve.genes.GetGene(geneQuality);
                if (gene != null)
                {
                    curSleeve.genes.RemoveGene(gene);
                }
            }
            geneQuality = ACUtils.sleeveQualities[sleeveQualityIndex];
            curSleeve.genes.AddGene(geneQuality, false);
        }

        //public static string GetHediffToolTip(IEnumerable<Hediff> diffs, Pawn pawn)
        //{
        //    string str = "";
        //    foreach (Hediff hediff in diffs)
        //    {
        //        str += hediff.GetTooltip(pawn, false) + "\n";
        //    }
        //    return str;
        //}

        public void RecheckEverything()
        {
            RecheckBodyOptions();
            InitializeIndexes();
            UpdateGrowCost();
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

            if (curSleeve.story.hairDef != null && permittedHairs.Contains(curSleeve.story.hairDef) is false)
            {
                curSleeve.story.hairDef = permittedHairs.RandomElement();
            }
            if (curSleeve.style.beardDef != null && permittedBeards.Contains(curSleeve.style.beardDef) is false)
            {
                curSleeve.style.beardDef = permittedBeards.RandomElement();
            }
        }

        public string ExtractHeadLabels(string headLabel)
        {
            headLabel = Regex.Replace(headLabel, @"^[A-Z]+_", @"");
            headLabel = Regex.Replace(headLabel, @"^[A-Z]+([A-Z])", @"$1");
            headLabel = headLabel.Replace("_", " ");
            headLabel = headLabel.SplitCamelCase();
            headLabel = Regex.Replace(headLabel, @" +", " ");
            headLabel = headLabel.FirstCharToUpper();
            return headLabel;
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
                DefDatabase<BodyTypeDef>.AllDefsListForReading.Where(x => x.modContentPack?.IsOfficialMod ?? false)).Except(invalidBodies).ToList();
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
            var list = (ModCompatibility.AlienRacesIsActive ?
                    ModCompatibility.GetAllowedHeadTypes(curSleeve.def) :
                    DefDatabase<HeadTypeDef>.AllDefs.Where(x => x.modContentPack?.IsOfficialMod ?? false).Except(invalidHeads)).Where(x => CanUseHeadType(x)).ToList();
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

        public void LoadSleeve(SleevePreset preset)
        {
            curXenogerm = null;
            curSleeve = preset.sleeve;
            var xenogenes = curSleeve.genes.Xenogenes.Select(x => x.def).ToList();
            if (xenogenes.Any())
            {
                curXenogerm = TryFindXenogerm(xenogenes);
                if (curXenogerm is null)
                {
                    foreach (var gene in curSleeve.genes.Xenogenes.ToList())
                    {
                        curSleeve.genes.RemoveGene(gene);
                    }
                    Messages.Message("AC.SleeveDesignCouldntBeFullyLoaded".Translate(), MessageTypeDefOf.CautionInput);
                }
            }
            currentPawnKindDef = curSleeve.kindDef;
            InitializeIndexes();
        }

        private Xenogerm TryFindXenogerm(List<GeneDef> xenogenes)
        {
            foreach (var xenogerm in sleeveGrower.Map.GetXenogerms())
            {
                var genes = xenogerm.GeneSet.GenesListForReading;
                if (genes.Count == xenogenes.Count && xenogenes.OrderBy(x => x.defName).SequenceEqual(genes.OrderBy(x => x.defName).ToList()))
                {
                    return xenogerm;
                }
            }
            return null;
        }

        private void InitializeIndexes()
        {
            hairTypeIndex = GetPermittedHairs().IndexOf(curSleeve.story.hairDef);
            beardTypeIndex = GetPermittedBeards().IndexOf(curSleeve.style.beardDef);
            headTypeIndex = GetPermittedHeads().Select(x => x.Value).ToList().IndexOf(curSleeve.story.headType);
            indexesPerCategory = new Dictionary<string, int>();
            var genes = curSleeve.genes.GenesListForReading.Where(x => x.def.exclusionTags.NullOrEmpty() is false).Select(x => x.def).ToList();
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

            if (ModCompatibility.AlienRacesIsActive)
            {
                raceTypeIndex = ModCompatibility.GetPermittedRaces().IndexOf(curSleeve.def);
            }

            if (curSleeve.gender == Gender.Male)
            {
                maleBodyTypeIndex = GetPermittedBodyTypes().Select(x => x.Value).ToList().IndexOf(curSleeve.story.bodyType);
            }
            else if (curSleeve.gender == Gender.Female)
            {
                femaleBodyTypeIndex = GetPermittedBodyTypes().Select(x => x.Value).ToList().IndexOf(curSleeve.story.bodyType);
            }

            foreach (var gene in ACUtils.sleeveQualities)
            {
                if (curSleeve.genes.GetGene(gene) != null)
                {
                    sleeveQualityIndex = ACUtils.sleeveQualities.IndexOf(gene);
                }
            }
        }
        private void CreateSleeve(Gender gender)
        {
            curXenogerm = null;
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
            curSleeve = PawnGenerator.GeneratePawn(new PawnGenerationRequest(currentPawnKindDef, Faction.OfPlayer, PawnGenerationContext.NonPlayer,
                -1, true, false, false, false, false, 0f, false, true, true, false, false, false, true,
                fixedGender: gender, forcedXenotype: XenotypeDefOf.Baseliner));
            curSleeve.DestroyGear();
            curSleeve.MakeEmptySleeve();
            var lastAdultAge = curSleeve.RaceProps.lifeStageAges.LastOrDefault((LifeStageAge lifeStageAge) => lifeStageAge.def.developmentalStage.Adult())?.minAge ?? 0f;
            curSleeve.ageTracker.AgeBiologicalTicks = (long)Mathf.FloorToInt(lastAdultAge * 3600000f);
            curSleeve.ageTracker.AgeChronologicalTicks = (long)Mathf.FloorToInt(lastAdultAge * 3600000f);
            curSleeve.health = new Pawn_HealthTracker(curSleeve);
            curSleeve.Rotation = Rot4.South;
            convertedGenes = new List<Gene>();
            ApplyGeneQuality();
            RecheckEverything();
            if (curSleeve.genes.CustomXenotype != null)
            {
                curSleeve.genes.CustomXenotype.inheritable = true;
            }
        }
    }
}
