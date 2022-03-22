using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;

using static ExtraStats.Util;
using static ExtraStats.HarmonyPatches;
using UnityEngine;
using static Verse.Dialog_InfoCard;
using System.Reflection.Emit;

#pragma warning disable IDE0051 // Remove unused private members
namespace ExtraStats
    {
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
        {
        public static float maxUsableWindIntensity;
        public static string fullSunPower;
        public const string W = " W";
        public const string Wh = " Wh";
        static HarmonyPatches()
            {
            //Harmony.DEBUG = true;

            var harmony = new Harmony("princess.extrastats");
            Log.Message("ExtraStats patching...");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            var statRepUtil_StatsToDraw_anon_original = AccessTools.FirstMethod(
                AccessTools.FirstInner(typeof(StatsReportUtility),
                inner => inner.GetField("def", AccessTools.all) != null),
                method => method.Name.Contains("MoveNext"));

            var statRepUtil_StatsToDraw_anon_transpiler = typeof(extraStats_StatsReportUtility_DrawStatsReport_anon_Patch).GetMethod("Transpiler", AccessTools.all);
            harmony.Patch(statRepUtil_StatsToDraw_anon_original, transpiler: new HarmonyMethod(statRepUtil_StatsToDraw_anon_transpiler));

            var hm = new HarmonyMethod(typeof(extraStats_QualityCategory_Transpiler_Patch).GetMethod("Transpiler", AccessTools.all));
            harmony.Patch(statRepUtil_StatsToDraw_anon_original, transpiler: hm);
            harmony.Patch(AccessTools.Method(typeof(StatsReportUtility), "DrawStatsReport", new Type[] { typeof(Rect), typeof(Def), typeof(ThingDef) }), transpiler: hm);
            harmony.Patch(AccessTools.Method(typeof(StatsReportUtility), "StatsToDraw", new Type[] { typeof(Def), typeof(ThingDef) }), transpiler: hm);

            maxUsableWindIntensity = (float)AccessTools.Field(typeof(CompPowerPlantWind), "MaxUsableWindIntensity").GetValue(1.569f);
            fullSunPower = ((float)AccessTools.Field(typeof(CompPowerPlantSolar), "FullSunPower").GetValue(1690f)).ToString("F0") + W;
            }
        }

    [HarmonyPatch(typeof(Dialog_InfoCard), "GetTitle")]
    public static class extraStats_InfoCard_GetTitle_Patch
        {
        static string Postfix(string ret, Dialog_InfoCard __instance, Def ___def)
            {
            if (___def != null && ___def is ThingDef def && def.HasComp(typeof(CompQuality)))
                {
                return ret + " (" + extraStats_InfoCard_Patch.category.GetLabel() + ")";
                }
            return ret;
            }
        }

    [HarmonyPatch(typeof(Dialog_InfoCard), "DoWindowContents")]
    public static class extraStats_InfoCard_Patch
        {
        public static QualityCategory category = QualityCategory.Normal;
        private static readonly MethodInfo setup = AccessTools.Method(typeof(Dialog_InfoCard), "Setup");

        static void Postfix(Dialog_InfoCard __instance, Rect inRect, Def ___def, List<Hyperlink> ___history)
            {
            if (___def != null && ___def is ThingDef def && def.HasComp(typeof(CompQuality)))
                {
                if (ShowQualityButton(inRect, ___history.Count > 0, def.MadeFromStuff))
                    {
                    List<FloatMenuOption> list = new List<FloatMenuOption>();
                    foreach (QualityCategory category in (QualityCategory[])Enum.GetValues(typeof(QualityCategory)))
                        {
                        QualityCategory local = category;
                        list.Add(new FloatMenuOption(category.GetLabel(), delegate
                            {
                                // FIXME: All hail storing things in static variables.
                                extraStats_InfoCard_Patch.category = local;
                                setup.Invoke(__instance, new object[] { });
                                }));
                        }
                    Find.WindowStack.Add(new FloatMenu(list));
                    }
                }
            else extraStats_InfoCard_Patch.category = QualityCategory.Normal;

            //Widgets.Label(new Rect(inRect.x + 200f, inRect.y + 18f, 200f, 40f), extraStats_InfoCard_Patch.category.GetLabel());
            }
        private static bool ShowQualityButton(Rect containerRect, bool withBackButtonOffset, bool stuffed)
            {
            float num = containerRect.x + containerRect.width - 14f - 200f - 16f - (stuffed ? 200f + 8f : 0f);
            if (withBackButtonOffset)
                {
                num -= 136f;
                }
            return Widgets.ButtonText(new Rect(num, containerRect.y + 18f, 200f, 40f), "princess.ExtraStats.showQuality".Translate());
            }
        }

    static class extraStats_QualityCategory_Transpiler_Patch
        {
        public static MethodInfo srfor = AccessTools.Method(typeof(StatRequest), "For", new Type[] { typeof(BuildableDef), typeof(ThingDef), typeof(QualityCategory) });
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
            {
            return instructions.MethodReplacer(srfor, AccessTools.Method(typeof(Util), nameof(StatRequestFor)));
            }
        }


    static class extraStats_StatsReportUtility_DrawStatsReport_anon_Patch
        {
        private static readonly Type statReqCont = AccessTools.FirstInner(typeof(StatsReportUtility),
                inner => inner.Name.Contains("Class20"));
        private static readonly Type that = AccessTools.FirstInner(typeof(StatsReportUtility),
                inner => inner.GetField("def", AccessTools.all) != null);

        private static readonly MethodInfo srfor = AccessTools.Method(typeof(StatExtension), "GetStatValueAbstract", new Type[] { typeof(BuildableDef), typeof(StatDef), typeof(ThingDef) });
        private static readonly MethodInfo callVirt = AccessTools.Method(typeof(StatWorker), "GetValue", new Type[] { typeof(StatRequest), typeof(bool) });

        private static readonly FieldInfo bDef = AccessTools.GetDeclaredFields(that).First(info => info.FieldType == typeof(BuildableDef));

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
            {
            var instructionsList = instructions.ToList();

            Label returnValue = il.DefineLabel();
            // LocalBuilder statReqContLocal = il.DeclareLocal(statReqCont);

            // init the third local
            yield return new CodeInstruction(OpCodes.Newobj, AccessTools.Constructor(statReqCont));
            yield return new CodeInstruction(OpCodes.Stloc_3);

            // init bDef
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldfld, that.GetField("def", AccessTools.all));
            yield return new CodeInstruction(OpCodes.Isinst, typeof(BuildableDef));
            yield return new CodeInstruction(OpCodes.Stfld, bDef);
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldfld, bDef);
            yield return new CodeInstruction(OpCodes.Brfalse, returnValue);

            // init statRequest
            yield return new CodeInstruction(OpCodes.Ldloc_3);
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldfld, bDef);
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldfld, that.GetField("stuff", AccessTools.all));
            yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(extraStats_InfoCard_Patch), nameof(extraStats_InfoCard_Patch.category)));
            yield return new CodeInstruction(OpCodes.Call, extraStats_QualityCategory_Transpiler_Patch.srfor);
            yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(statReqCont, "statRequest"));

            yield return new CodeInstruction(OpCodes.Nop) { labels = new List<Label> { returnValue } };

            for (var i = 0; i < instructionsList.Count(); i++)
                {
                var item = instructionsList[i];
                if (i < instructionsList.Count() - 6 && // making sure not to OOB
                    item.opcode == OpCodes.Ldarg_0 &&
                    instructionsList[i + 5].Calls(srfor) &&
                    // Extra insanity checks
                    instructionsList[i + 1].opcode == OpCodes.Ldfld &&
                    instructionsList[i + 4].opcode == OpCodes.Ldfld)
                    {
                    //item.Worker.GetValue(statRequest)
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 5);
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(StatDef), "get_Worker"));
                    // yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloc_3);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(statReqCont, "statRequest"));
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                    yield return new CodeInstruction(OpCodes.Callvirt, callVirt);

                    i += 5;
                    }
                else yield return item;
                }
            }
        }

    [HarmonyPatch(typeof(CompProperties), "SpecialDisplayStats")]
    static class extraStats_God_Properties_Patch
        {
        static IEnumerable<StatDrawEntry> Postfix(IEnumerable<StatDrawEntry> ret, CompProperties __instance)
            {
            if (!ret.EnumerableNullOrEmpty())
                {
                foreach (StatDrawEntry item in ret)
                    {
                    yield return item;
                    }
                }

            if (__instance is CompProperties_Power compPower)
                {
                if (compPower is CompProperties_Battery compBattery)
                    {
                    yield return buildingStat("storedEnergyMax", compBattery.storedEnergyMax.ToString("F0") + Wh, 4949);
                    yield return buildingStat("efficiency", compBattery.efficiency.ToStringPercent(), 4945);

                    }
                if (compPower.basePowerConsumption < 0f)
                    yield return buildingStat("basePowerConsumption", (-compPower.basePowerConsumption).ToString("F0") + W, 5000);
                if (compPower.compClass == typeof(CompPowerPlantSolar))
                    yield return buildingStat("solarPowerOutput", fullSunPower, 4995);
                else if (compPower.compClass == typeof(CompPowerPlantWind))
                    yield return buildingStat("windPowerOutput", (-compPower.basePowerConsumption * maxUsableWindIntensity).ToString("F0") + W, 4995);

                if (compPower.transmitsPower)
                    yield return buildingStat("transmitsPower", compPower.transmitsPower.ToStringYesNo(), 4920);
                yield return buildingStat("shortCircuitInRain", compPower.shortCircuitInRain.ToStringYesNo(), 4910);
                }
            else if (__instance is CompProperties_Breakdownable)
                {
                yield return buildingStat("breakdownable", true.ToStringYesNo(), 69);
                }
            }
        }
    /*
    [HarmonyPatch(typeof(ThingComp), "SpecialDisplayStats")]
    static class extraStats_God_Comp_Patch
        {
        static IEnumerable<StatDrawEntry> Postfix(IEnumerable<StatDrawEntry> ret, ThingComp __instance)
            {
            if (!ret.EnumerableNullOrEmpty())
                {
                foreach (StatDrawEntry item in ret)
                    {
                    yield return item;
                    }
                }
            }
        }

    [HarmonyPatch(typeof(Thing), "SpecialDisplayStats")]
    static class extraStats_God_Thing_Patch
        {
        static IEnumerable<StatDrawEntry> Postfix(IEnumerable<StatDrawEntry> ret, Thing __instance)
            {
            if (!ret.EnumerableNullOrEmpty())
                {
                foreach (StatDrawEntry item in ret)
                    {
                    yield return item;
                    }
                }
            }
        }
     */

    [HarmonyPatch(typeof(BuildableDef), "SpecialDisplayStats")]
    static class extraStats_God_BuildableDef_Patch
        {
        static IEnumerable<StatDrawEntry> Postfix(IEnumerable<StatDrawEntry> ret, BuildableDef __instance, StatRequest req)
            {
            if (!ret.EnumerableNullOrEmpty())
                {
                foreach (StatDrawEntry item in ret)
                    {
                    yield return item;
                    }
                }
            if (StatDefOf.ShootingAccuracyTurret.Worker.ShouldShowFor(req))
                {
                // __result = __result.AddItem()
                yield return new StatDrawEntry(StatDefOf.ShootingAccuracyTurret.category, StatDefOf.ShootingAccuracyTurret.labelForFullStatList, StatDefOf.ShootingAccuracyPawn.postProcessCurve.EvaluateInverted(__instance.GetStatValueAbstract(StatDefOf.ShootingAccuracyTurret)).ToString("F2"), StatDefOf.ShootingAccuracyTurret.description, StatDefOf.ShootingAccuracyTurret.displayPriorityInCategory - 1);
                }

            if (StatDefOf.RangedWeapon_Cooldown.Worker.ShouldShowFor(req))
                {
                yield return new StatDrawEntryDPS(StatCategoryDefOf.Weapon_Ranged, "princess.ExtraStats.DPSGraph".Translate(), "", "princess.ExtraStats.DPSGraph.Desc".Translate(), 5500 - 1);

                yield return new StatDrawEntryAccuracy(StatCategoryDefOf.Weapon_Ranged, "princess.ExtraStats.AccuracyGraph".Translate(), "", "princess.ExtraStats.AccuracyGraph.Desc".Translate(), StatDefOf.AccuracyLong.displayPriorityInCategory - 1);
                }
            }
        }

    public static class Util
        {
        public static StatDrawEntry buildingStat(string stat, string input, int priority)
            {
            return new StatDrawEntry(StatCategoryDefOf.Building, ("princess.ExtraStats." + stat).Translate(),
                    input, ("princess.ExtraStats." + stat + ".desc").Translate(), priority);
            }

        public static StatRequest StatRequestFor(BuildableDef def, ThingDef stuffDef, QualityCategory quality = QualityCategory.Normal)
            {
            if (def is ThingDef tDef && tDef.HasComp(typeof(CompQuality)))
                {
                if (quality == QualityCategory.Normal) quality = extraStats_InfoCard_Patch.category;

                var thing = ThingMaker.MakeThing(tDef, stuffDef);
                thing.TryGetComp<CompQuality>()?.SetQuality(quality, ArtGenerationContext.Outsider);

                var req = StatRequest.For(thing);

                req.Thing.Destroy();
                if (!req.Thing.Discarded) req.Thing.Discard(true);

                return req;
                }
            return StatRequest.For(def, stuffDef, quality);
            }
        }
    }