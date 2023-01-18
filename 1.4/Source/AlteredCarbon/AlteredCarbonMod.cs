﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace AlteredCarbon
{
    class AlteredCarbonMod : Mod
    {
        public static AlteredCarbonSettings settings;
        public AlteredCarbonMod(ModContentPack pack) : base(pack)
        {
            settings = GetSettings<AlteredCarbonSettings>();
            Log.Message("Altered Carbon 2.0 is active");
        }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            settings.DoSettingsWindowContents(inRect);
        }

        // Return the name of the mod in the settings tab in game
        public override string SettingsCategory()
        {
            return "Altered Carbon";
        }
    }
}
