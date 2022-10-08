using HarmonyLib;
using HugsLib;
using HugsLib.Settings;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace ExtraStats
    {
    public class StatDrawEntryAccuracy : StatDrawEntry
        {
        public StatDrawEntryAccuracy(StatCategoryDef category, string label, string valueString, string reportText, int displayPriorityWithinCategory, string overrideReportTitle = null, IEnumerable<Dialog_InfoCard.Hyperlink> hyperlinks = null, bool forceUnfinalizedMode = false) : base(category, label, valueString, reportText, displayPriorityWithinCategory, overrideReportTitle, hyperlinks, forceUnfinalizedMode)
            {}

        public static Dictionary<(ThingDef, QualityCategory, int), Texture2D> renderedStats = new Dictionary<(ThingDef, QualityCategory, int), Texture2D>();

        public static int minLevel = -5;
        public static int maxLevel = 30;
        public static int scale = 12;

        public static int beyond = 2;

        private static Vector2 scrollPosition;

        public static List<Color> defaultSpectrum = new List<Color>
            {
            new Color(0.34375f, 0.0625f, 0f),
            new Color(1f, 0.8125f, 0.40625f),
            new Color(0f, 1f, 1f),

            new Color(0f, 0.625f, 0.90625f),
            new Color(0f, 0f, 1f),
            new Color(1f, 0.5f, 1f)
            };

        public static List<Color> spectrum;
        public static Texture2D bar;
        public static Texture2D barBeyond;

        public static float Render(float currentY, Rect windowRect, Thing thing, StatDrawEntry statIn)
            {
            if (thing != null && thing.TryGetVerb() != null && statIn is StatDrawEntryAccuracy stat)
                {
                Texture2D texture;
                QualityCategory quality;
                thing.TryGetQuality(out quality);

                int minRange = Mathf.CeilToInt(thing.TryGetVerb().minRange);
                int maxRange = Mathf.CeilToInt(thing.TryGetVerb().range);
                    
                if (!renderedStats.TryGetValue((thing.def, quality, stat.GetTypeHash()), out texture))
                    {
                    texture = new Texture2D(maxRange, (maxLevel - minLevel + 1), TextureFormat.ARGB32, false);
                    texture.filterMode = FilterMode.Point;

                    for (int range = 1; range <= maxRange; range++)
                        {
                        //levels are off by minLevel, for texture creation reasons
                        //also printed backwards, because textures
                        for (int level = 0; level <= maxLevel - minLevel; level++)
                            {
                            float accuracy = accuracyFromShooterGunAndDist(thing, level + minLevel, range);
                            accuracy = stat.transformAccuracy(accuracy, thing, level + minLevel);

                            if (range < minRange || accuracy <= 0f)
                                texture.SetPixel(range - 1, maxLevel - minLevel - level, Color.black.ToTransparent(0));
                            else
                                {
                                texture.SetPixel(range - 1, maxLevel - minLevel - level, ColorsFromSpectrum.Get(spectrum, accuracy / beyond));
                                }
                            }
                        }

                    texture.Apply();

                    renderedStats.Add((thing.def, quality, stat.GetTypeHash()), texture);
                    }
                if (bar == null)
                    {
                    bar = new Texture2D(scale * 8 * 2, scale, TextureFormat.ARGB32, false);
                    for (float i = 0; i < bar.width; i++)
                        for (int j = 0; j < bar.height; j++)
                            {
                            bar.SetPixel((int)i, j, ColorsFromSpectrum.Get(spectrum, i / bar.width / beyond));
                            }
                    bar.Apply();

                    barBeyond = new Texture2D(scale * 8 * 2 * beyond, scale, TextureFormat.ARGB32, false);
                    for (float i = 0; i < barBeyond.width; i++)
                        for (int j = 0; j < barBeyond.height; j++)
                            {
                            barBeyond.SetPixel((int)i, j, ColorsFromSpectrum.Get(spectrum, i / barBeyond.width));
                            }
                    barBeyond.Apply();
                    }


                // Very important numbers
                float margin = 25f;
                float largeMargin = margin * 2;
                float smallMargin = 10f;

                TextAnchor anchor = Text.Anchor;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Tiny;
                var tinyTextHeight = Text.CalcHeight("65535", largeMargin);
                Widgets.Label(new Rect(0, currentY, largeMargin, margin), stat.label(0.000001f));
                Text.Anchor = TextAnchor.UpperRight;
                var baRect = new Rect(0, currentY + tinyTextHeight, bar.width, bar.height);
                if (stat.goesBeyond() && Mouse.IsOver(baRect))
                    {
                    Widgets.Label(new Rect(barBeyond.width - largeMargin, currentY, largeMargin, margin), stat.label(beyond));
                    Text.Anchor = TextAnchor.UpperCenter;
                    Widgets.Label(barBeyond.width / beyond - margin, ref currentY, largeMargin, stat.label(1));
                    }
                else
                    Widgets.Label(bar.width - largeMargin, ref currentY, largeMargin, stat.label(1));
                Text.Anchor = anchor;
                Text.Font = GameFont.Small;


                if (stat.goesBeyond() && Mouse.IsOver(baRect))
                    Widgets.DrawTextureFitted(new Rect(0, currentY, barBeyond.width, barBeyond.height), barBeyond, 1);
                else
                    Widgets.DrawTextureFitted(baRect, bar, 1);
                currentY += bar.height;

                stat.renderInfo(ref currentY, thing);


                Widgets.BeginScrollView(new Rect(0, currentY, windowRect.width / 2, texture.height * scale + largeMargin), ref scrollPosition, new Rect(0, currentY, texture.width * scale + largeMargin + margin, texture.height * scale + margin));

                Text.Font = GameFont.Tiny;
                var mod = 1;
                if (scale < tinyTextHeight)
                    mod = 5;
                Widgets.Label(new Rect(margin, currentY, texture.width * scale, margin), "princess.ExtraStats.Range".Translate().CapitalizeFirst());
                currentY += 13;
                for (int i = minRange > 0 ? minRange : 1; i <= maxRange; i++)
                    if (i % mod == 0) 
                        Widgets.Label(new Rect(margin + (i - 1) * scale, currentY - 1, margin, margin), i.ToString());
                currentY += smallMargin;
                Widgets.Label(new Rect(0, currentY, 8, texture.height * scale), "princess.ExtraStats.AccuracyLevel".Translate().CapitalizeFirst());
                for (int i = minLevel; i <= maxLevel; i++)
                    if (i % mod == 0)
                        Widgets.Label(new Rect(13, currentY + (i - minLevel) * scale, margin, margin), i.ToString());
                currentY += 2;
                Text.Font = GameFont.Small;

                Rect texRect = new Rect(margin, currentY, texture.width * scale, texture.height * scale);
                Widgets.DrawTextureFitted(texRect, texture, 1f);
                if (thing is Building_Turret)
                    {
                    var level = StatDefOf.ShootingAccuracyPawn.postProcessCurve.EvaluateInverted(thing.GetStatValue(StatDefOf.ShootingAccuracyTurret)) - minLevel;
                    Widgets.DrawBox(new Rect(margin, currentY + level * scale, texture.width * scale, scale));
                    }
                else if (thing.ParentHolder != null && thing.ParentHolder is Pawn_EquipmentTracker pe)
                    {
                    var level = StatDefOf.ShootingAccuracyPawn.postProcessCurve.EvaluateInverted(pe.pawn.GetStatValue(StatDefOf.ShootingAccuracyPawn)) - minLevel;
                    Widgets.DrawBox(new Rect(margin, currentY + level * scale, texture.width * scale, scale));
                    }
                if (Mouse.IsOver(texRect))
                    {
                    var offset = new Vector2(margin, currentY);
                    var mousePos = (Event.current.mousePosition - offset).RoundedTo(scale) + offset;
                    var drawrect = new Rect(mousePos, Vector2.one * scale);
                    var dataVec = ((Event.current.mousePosition - offset) / scale).Rounded();
                    dataVec.x += 1;
                    dataVec.y += minLevel;
                    var rectLevel = new Rect(drawrect);
                    rectLevel.x = smallMargin;
                    rectLevel.width = margin - smallMargin + texture.width * scale;
                    var rectAcc = new Rect(drawrect);
                    rectAcc.y = currentY - margin + smallMargin;
                    rectAcc.height = margin - smallMargin + texture.height * scale;

                    Widgets.DrawLightHighlight(rectLevel);
                    Widgets.DrawLightHighlight(rectAcc);
                    Widgets.DrawHighlight(drawrect);

                    VectorLabel(mousePos, stat.label(stat.transformAccuracy(accuracyFromShooterGunAndDist(thing, dataVec), thing, dataVec.y)));
                    }
                currentY += texture.height * scale;

                Widgets.EndScrollView();
                currentY += margin;
                }
            else if (statIn is StatDrawEntryAccuracy) Widgets.Label(0, ref currentY, 100f, (thing?.def.IsRangedWeapon.ToString() ?? "null") + ":" + thing?.GetType().Name);

            return currentY;
            }

        public static float accuracyFromShooterGunAndDist(Thing gun, Vector2 data)
            {
            return accuracyFromShooterGunAndDist(gun, data.y, data.x);
            }

        public static float accuracyFromShooterGunAndDist(Thing gun, float level, float range)
            {/*
            if (!ExtraStatsSettings.extremeCompatMode)
                {*/
                if (range < gun.TryGetVerb().minRange) return 0;
                float accuracy = ShotReport.HitFactorFromShooter(StatDefOf.ShootingAccuracyPawn.postProcessCurve.Evaluate(level), range);
                accuracy *= gun.TryGetVerb().GetHitChanceFactor(gun.GetGun(), range);
                return Mathf.Clamp(accuracy, 0.0201f, 1f);/*
                }
            else return extremeHeresy(gun, level, range);*/
            }
        /*
        private static float extremeHeresy(Thing gun, float level, float range) 
            {
            // Genuine Yayo code here, test purposes only

            float srAccuracy = ShotReport.HitFactorFromShooter(StatDefOf.ShootingAccuracyPawn.postProcessCurve.Evaluate(level), range);
            srAccuracy *= gun.TryGetVerb().GetHitChanceFactor(gun.GetGun(), range);
            srAccuracy = Mathf.Clamp(srAccuracy, 0.0201f, 1f);

            float missR = (1f - srAccuracy * 0.5f);
            if (missR < 0f) missR = 0f;

            float factorStat = 0.95f;
            float factorSkill = level / 20f;
            factorStat = 1f - (factorStat * factorSkill);

            float factorEquip = 1f - gun.TryGetVerb().GetHitChanceFactor(gun.GetGun(), range);

            float factorGas = 1f;

            float factorWeather = 1f;

            float factorAir = factorGas * factorWeather;

            missR *= (0.6f * factorStat + (1f - 0.6f)) * factorAir + (1f - factorAir);

            if (range < 10f)
                {
                missR -= Mathf.Clamp((10f - range) * 0.07f, 0f, 0.3f);
                }

            missR = missR * 0.95f + 0.05f;
            Mathf.Clamp(missR, 0.05f, 0.95f);

            return Mathf.Clamp01(1f - missR);
            }*/
        public static void VectorLabel(Vector2 position, string label)
            {
            Rect rect = new Rect(position.x - 8f, position.y - 24f, 9999f, 100f);
            Vector2 vector = Text.CalcSize(label);
            rect.height = Mathf.Max(rect.height, vector.y);
            GUI.DrawTexture(new Rect(rect.x - vector.x * 0.1f, rect.y, vector.x * 1.2f, vector.y), TexUI.GrayTextBG);
            Widgets.Label(rect, label);
            }


        public virtual void renderInfo(ref float currentY, Thing thing) {}
        public virtual string label(float acc)
            {
            return acc.ToStringPercentEmptyZero("F1");
            }
        public virtual float transformAccuracy(float acc, Thing thing, float level)
            {
            return acc;
            }
        public virtual int GetTypeHash()
            {
            return GetType().GetHashCode();
            }
        public virtual bool goesBeyond()
            {
            return false;
            }
        }

    public class StatDrawEntryDPS : StatDrawEntryAccuracy
        {
        public static int maxDPS = 20;
        private static float armor = 0;
        public StatDrawEntryDPS(StatCategoryDef category, string label, string valueString, string reportText, int displayPriorityWithinCategory, string overrideReportTitle = null, IEnumerable<Dialog_InfoCard.Hyperlink> hyperlinks = null, bool forceUnfinalizedMode = false) : base(category, label, valueString, reportText, displayPriorityWithinCategory, overrideReportTitle, hyperlinks, forceUnfinalizedMode)
            { }
        public override void renderInfo(ref float currentY, Thing thing)
            {
            armor = Widgets.HorizontalSlider(new Rect(0, currentY, bar.width, 31f), armor, 0, 2,
                label: labelForArmorType(thing), leftAlignedLabel: "0%", rightAlignedLabel: "200%", roundTo: 0.02f);
            Widgets.Label(new Rect(bar.width + 10f, currentY, 100f, 25f), armor.ToStringPercent());
            currentY += 31f;
            }
        public override string label(float acc)
            {
            return (acc * maxDPS).ToStringEmptyZero("F2");
            }
        public override float transformAccuracy(float acc, Thing thing, float level)
            {
            var verb = thing.TryGetVerb();

            var effArmor = Math.Max(armor - verb.defaultProjectile?.projectile.GetArmorPenetration(thing) ?? 0, 0);
            var pierce = 1 - (effArmor <= 1 ? effArmor * 0.75f : 0.75f + (effArmor - 1) * 0.25f);

            return acc /
                maxDPS *
                pierce *
                (verb.defaultProjectile?.projectile?.GetDamageAmount(thing) ?? 1) *
                verb.burstShotCount /
                (verb.warmupTime +
                thing.GetRangedCooldown(level) +
                (verb.ticksBetweenBurstShots / GenTicks.TicksPerRealSecond) *
                (verb.burstShotCount - 1));
            }
        public override int GetTypeHash()
            {
            return GetType().GetHashCode() + Mathf.RoundToInt(armor * 100);
            }
        public override bool goesBeyond()
            {
            return true;
            }

        private static string labelForArmorType(Thing thing) 
            {
            return thing.TryGetVerb()?.defaultProjectile?.projectile?.damageDef?.armorCategory?.armorRatingStat.label;
            }

        }

    public static class Extensions 
        {
        public static void SetMetaPixel(this Texture2D texture, int x, int y, Color color, int size)
            {
            for (int i = 0; i < size; i++)
                for (int j = 0; j < size; j++)
                    {
                    texture.SetPixel(x * size + i, y * size + j, color);
                    }
            }

        public static Vector2 RoundedTo(this Vector2 vec, int scale)
            {
            vec /= scale;
            vec = vec.Rounded();
            vec *= scale;
            return vec;
            }

        public static VerbProperties TryGetVerb(this Thing thing) 
            {
            if (thing.def.IsRangedWeapon)
                return thing.def.Verbs[0];
            if (thing is Building_Turret bt)
                return bt.AttackVerb?.verbProps;
            return null;
            }

        public static float GetRangedCooldown(this Thing thing, float acc)
            {
            if (thing.def.IsRangedWeapon)
                return thing.GetStatValue(StatDefOf.RangedWeapon_Cooldown);
            if (thing is Building_Turret bt)
                return bt.def.building.turretBurstCooldownTime;
            return 0f;
            }
        public static Thing GetGun(this Thing thing)
            {
            if (thing.def.IsRangedWeapon)
                return thing;
            if (thing is Building_TurretGun bt && bt.AttackVerb.DirectOwner is CompEquippable ce)
                return ce.parent;
            return null;
            }
        }

    [HarmonyPatch(typeof(StatsReportUtility), "DrawStatsReport", new Type[] { typeof(Rect), typeof(Def), typeof(ThingDef) })]
    public static class extraStats_StatsReportUtility_DrawStatsReport_DSW_Patch
        {
        private static readonly MethodInfo lfr = AccessTools.Method(typeof(StatsReportUtility), "DrawStatsWorker");
        private static readonly MethodInfo make = AccessTools.Method(typeof(extraStats_StatsReportUtility_DrawStatsReport_DSW_Patch), "makeTempThingFrom");
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructionsIn, ILGenerator il)
            {
            var instructions = instructionsIn.ToList();
            for (int i = 0; i < instructions.Count; i++)
                {
                if (i + 2 < instructions.Count &&
                    instructions[i].opcode == OpCodes.Ldnull &&
                    instructions[i+2].Calls(lfr))
                    {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Ldarg_2);
                    yield return new CodeInstruction(OpCodes.Call, make);
                    }
                else yield return instructions[i];
                }
            }

        public static Thing makeTempThingFrom(Def def, ThingDef stuff)
            {
            if (def is ThingDef tDef)
                {
                var thing = ThingMaker.MakeThing(tDef, stuff);
                if (tDef.HasComp(typeof(CompQuality)))
                    {
                    thing.TryGetComp<CompQuality>()?.SetQuality(extraStats_InfoCard_Patch.category, ArtGenerationContext.Outsider);
                    }

                thing.Destroy();
                if (!thing.Discarded) thing.Discard(true);

                return thing;
                }
            return null;
            }
        }

    [HarmonyPatch(typeof(StatsReportUtility), "DrawStatsWorker")]
    public static class extraStats_StatsReportUtility_DrawStatsWorker_Patch
        {
        private static readonly MethodInfo render = AccessTools.Method(typeof(StatDrawEntryAccuracy), "Render");
        private static readonly MethodInfo lfr = AccessTools.Method(typeof(Widgets), "BeginScrollView");
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
            {
            foreach (var instruction in instructions)
                {
                if (instruction.opcode == OpCodes.Stsfld &&
                    instruction.operand is FieldInfo field &&
                    field.Name == "rightPanelHeight")
                    {
                    // num2 currently on stack.
                    // StatDrawEntryAccuracy.Render(num2, optionalThing)
                    // The result is fed into `stsfld rightPanelHeight` directly.

                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 4);
                    yield return new CodeInstruction(OpCodes.Call, render);
                    }

                yield return instruction;
                }
            }
        }
    }
