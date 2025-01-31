﻿using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace AlteredCarbon
{
    [StaticConstructorOnStartup]
    public static class ACUtilsExtra
    {
        public static List<ThingDef> allGenepacks = new List<ThingDef>();
        static ACUtilsExtra()
        {
            Harmony harmony = new("AlteredCarbonExtra");
            harmony.PatchAll();
            foreach (var def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def.thingClass != null && typeof(Genepack).IsAssignableFrom(def.thingClass))
                {
                    allGenepacks.Add(def);
                }
            }
        }

        public static bool IsUltraTechBuilding(this ThingDef def)
        {
            if (typeof(Building).IsAssignableFrom(def.thingClass))
            {
                if (def.researchPrerequisites != null && def.researchPrerequisites.Any(x => HasRequisite(x, AC_Extra_DefOf.Xenogermination)))
                {
                    return true;
                }
                return def == AC_DefOf.VFEU_SleeveIncubator
                    || def == AC_DefOf.VFEU_SleeveCasket || def == AC_DefOf.VFEU_SleeveCasket
                    || def == AC_Extra_DefOf.AC_StackArray
                    || def == AC_DefOf.VFEU_DecryptionBench;
            }
            return false;
        }

        public static bool HasRequisite(ResearchProjectDef proj, ResearchProjectDef requirement)
        {
            if (proj == requirement)
            {
                return true;
            }
            else if (proj.prerequisites != null)
            {
                foreach (var research in proj.prerequisites)
                {
                    if (HasRequisite(research, requirement)) 
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static List<List<T>> ChunkBy<T>(this List<T> source, int chunkSize)
        {
            return source
                .Select((x, i) => new { Index = i, Value = x })
                .GroupBy(x => x.Index / chunkSize)
                .Select(x => x.Select(v => v.Value).ToList())
                .ToList();
        }
    }
}

