﻿using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using VFECore.Abilities;

namespace AlteredCarbon
{
    [HotSwappable]
    public class Hediff_CorticalStack : Hediff_Implant
    {
        public Ability_ArchoStackSkip skipAbility;
        public ThingDef SourceStack
        {
            get
            {
                if (this.def == AC_DefOf.VFEU_CorticalStack)
                {
                    return AC_DefOf.VFEU_FilledCorticalStack;
                }
                return AC_DefOf.AC_FilledArchoStack;
            }
        }
        private PersonaData personaData;
        public PersonaData PersonaData
        {
            get
            {
                if (personaData is null)
                {
                    personaData = new PersonaData();
                    personaData.CopyFromPawn(pawn, SourceStack, copyRaceGenderInfo: true);
                }
                return personaData;
            }
            set
            {
                personaData = value;
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (this.def == AC_DefOf.AC_ArchoStack)
            {
                if (skipAbility.ShowGizmoOnPawn())
                {
                    yield return skipAbility.GetGizmo();
                }
            }
            yield break;
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            this.Part = pawn.GetNeck();
            base.PostAdd(dinfo);

            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff != this && hediff is Hediff_CorticalStack otherStack)
                {
                    otherStack.preventKill = otherStack.preventSpawningStack = true;
                    pawn.health.RemoveHediff(otherStack);
                    otherStack.preventKill = otherStack.preventSpawningStack = false;
                }
            }

            var emptySleeveHediff = pawn.health.hediffSet.GetFirstHediffOfDef(AC_DefOf.VFEU_EmptySleeve);
            if (emptySleeveHediff != null)
            {
                pawn.health.RemoveHediff(emptySleeveHediff);
            }
            if (AlteredCarbonManager.Instance.PawnsWithStacks.Contains(pawn) is false)
            {
                AlteredCarbonManager.Instance.RegisterPawn(pawn);
                AlteredCarbonManager.Instance.TryAddRelationships(pawn, this.PersonaData.StackGroupData);
            }
            CreateSkipAbilityIfMissing();
        }

        private void CreateSkipAbilityIfMissing()
        {
            if (this.def == AC_DefOf.AC_ArchoStack && skipAbility is null)
            {
                skipAbility = (Ability_ArchoStackSkip)Activator.CreateInstance(AC_DefOf.AC_ArchoStackSkip.abilityClass);
                skipAbility.def = AC_DefOf.AC_ArchoStackSkip;
                skipAbility.holder = this.pawn;
                skipAbility.pawn = this.pawn;
                skipAbility.Init();
            }
        }

        public override bool ShouldRemove => false;
        public override void Notify_PawnDied()
        {
            if (!PersonaData.ContainsInnerPersona)
            {
                PersonaData.CopyFromPawn(this.pawn, SourceStack);
            }
            base.Notify_PawnDied();
        }

        public override void Notify_PawnKilled()
        {
            if (!PersonaData.ContainsInnerPersona)
            {
                PersonaData.CopyFromPawn(this.pawn, SourceStack);
            }
            base.Notify_PawnKilled();
        }

        public bool preventKill;
        public override void PostRemoved()
        {
            base.PostRemoved();
            if (!preventKill && !this.pawn.Dead)
            {
                this.pawn.Kill(null);
            }

            if (this.def == AC_DefOf.AC_ArchoStack)
            {
                if (!preventSpawningStack)
                {
                    SpawnStack(placeMode: ThingPlaceMode.Near);
                }
                pawn.health.hediffSet.hediffs.RemoveAll(x => x.def.defName == "VPE_PsycastAbilityImplant");
                pawn.health.hediffSet.hediffs.RemoveAll(x => x.def == HediffDefOf.PsychicAmplifier);
            }
        }
        public bool preventSpawningStack;
        public void SpawnStack(bool destroyPawn = false, ThingPlaceMode placeMode = ThingPlaceMode.Near, Caravan caravan = null, bool psycastEffect = false)
        {
            preventSpawningStack = true;
            var stackDef = SourceStack;
            var corticalStack = ThingMaker.MakeThing(stackDef) as CorticalStack;
            corticalStack.PersonaData.CopyFromPawn(this.pawn, stackDef);
            corticalStack.PersonaData.CopyOriginalData(PersonaData);
            if (this.pawn.MapHeld != null)
            {
                GenPlace.TryPlaceThing(corticalStack, this.pawn.PositionHeld, this.pawn.MapHeld, placeMode);
                if (psycastEffect)
                {
                    FleckMaker.Static(corticalStack.Position, corticalStack.Map, AC_DefOf.PsycastAreaEffect, 3f);
                }
            }
            else if (caravan != null)
            {
                CaravanInventoryUtility.GiveThing(caravan, corticalStack);
            }
            var degradationHediff = pawn.health.hediffSet.GetFirstHediff<Hediff_StackDegradation>();
            if (degradationHediff != null)
            {
                corticalStack.PersonaData.stackDegradation = degradationHediff.stackDegradation;
                pawn.health.RemoveHediff(degradationHediff);
            }
            pawn.health.RemoveHediff(this);
            AlteredCarbonManager.Instance.RegisterStack(corticalStack);
            AlteredCarbonManager.Instance.RegisterSleeve(this.pawn, corticalStack);
            if (destroyPawn)
            {
                if (this.pawn.Dead)
                {
                    this.pawn.Corpse.Destroy();
                }
                else
                {
                    this.pawn.Destroy();
                }
            }
            preventSpawningStack = false;
        }

        [HarmonyPatch(typeof(HediffSet), "ExposeData")]
        public static class HediffSet_ExposeData_Patch
        {
            public static Pawn curPawn;

            public static void Prefix(HediffSet __instance)
            {
                curPawn = __instance.pawn;
            }
            public static void Postfix(HediffSet __instance)
            {
                curPawn = null;
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref personaData, "personaData");
            Scribe_Deep.Look(ref skipAbility, "skipAbility");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                CreateSkipAbilityIfMissing();
            }
            var curPawn = this.pawn ?? HediffSet_ExposeData_Patch.curPawn;
            if (skipAbility != null)
            {
                skipAbility.holder = curPawn;
                skipAbility.pawn = curPawn;
            }
        }
    }
}