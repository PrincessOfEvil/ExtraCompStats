using HarmonyLib;
using HugsLib;
using HugsLib.Settings;
using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace ExtraStats
    {
    public class ExtraStatsSettings : ModBase
        {
        protected override bool HarmonyAutoPatch => false;

        public static SettingHandle<int> minLevel;
        public static SettingHandle<int> maxLevel;
        public static SettingHandle<int> scale;
        public static SettingHandle<int> beyond;
        public static SettingHandle<ColorArrayHandle> colors;
        /*
        public static SettingHandle<bool> extremeCompatMode;
        */
        public override string ModIdentifier
            {
            get { return "princess.ExtraStats"; }
            }
        public override void DefsLoaded()
            {
            Settings.ContextMenuEntries = new[] {
                                                    new ContextMenuEntry("princess.ExtraStats.dump".Translate(), () =>
                                                                             {
                                                                             if (StatDrawEntryAccuracy.renderedStats != null)
                                                                                 foreach (var texture in StatDrawEntryAccuracy.renderedStats)
                                                                                     {
                                                                                     var path = ModContentPack.RootDir + "/AccuracyImages/";
                                                                                     var key = texture.Key;
                                                                                     if (!Directory.Exists(path))
                                                                                         {
                                                                                         Directory.CreateDirectory(path);
                                                                                         }
                                                                                     Log.Message("[ExtraStats] Dumped all textures to: " + path);
                                                                                     File.WriteAllBytes(path + key.Item1.defName + key.Item2 + key.Item3 + ".png", texture.Value.EncodeToPNG());
                                                                                     }
                                                                             }),
                                                    new ContextMenuEntry("dumpEverything", () =>
                                                                             {
                                                                             foreach (var def in from x in DefDatabase<ThingDef>.AllDefsListForReading
                                                                                  where x.IsRangedWeapon
                                                                                  select x)
                                                                                 StatDrawEntryAccuracy.Render(0f, new Rect(), Util.StatRequestFor(def, null, QualityCategory.Legendary).Thing, new StatDrawEntryDPS(StatCategoryDefOf.Weapon_Ranged, "", "", "", 0));
                                                                             })
                                                    };

        minLevel = Settings.GetHandle(
                    "princess.ExtraStats.minLevel",
                    "princess.ExtraStats.minLevel".Translate(),
                    "princess.ExtraStats.minLevel.Desc".Translate(),
                    -5,
                    Validators.IntRangeValidator(-5, 0));
            maxLevel = Settings.GetHandle(
                    "princess.ExtraStats.maxLevel",
                    "princess.ExtraStats.maxLevel".Translate(),
                    "princess.ExtraStats.maxLevel.Desc".Translate(),
                    30,
                    Validators.IntRangeValidator(1, 60));

            scale = Settings.GetHandle(
                    "princess.ExtraStats.scale",
                    "princess.ExtraStats.scale".Translate(),
                    "princess.ExtraStats.scale.desc".Translate(),
                    12,
                    Validators.IntRangeValidator(1, 16));

            beyond = Settings.GetHandle(
                    "princess.ExtraStats.beyond",
                    "princess.ExtraStats.beyond".Translate(),
                    "princess.ExtraStats.beyond.Desc".Translate(),
                    2,
                    Validators.IntRangeValidator(2, 16));


            colors = Settings.GetHandle<ColorArrayHandle>(
                    "princess.ExtraStats.colors",
                    "princess.ExtraStats.colors".Translate(),
                    "princess.ExtraStats.colors.Desc".Translate());
            colors.CustomDrawerHeight = ColorArrayHandle.ELEMENT_HEIGHT * 3.5f;
            if (colors.Value == null) colors.Value = new ColorArrayHandle().initialize();
            colors.CustomDrawer = rect =>
                {
                if (colors.Value == null) colors.Value = new ColorArrayHandle().initialize();
                return ColorArrayHandle.customDrawer(rect, ref colors.Value.colorList);
                };
            if (StatDrawEntryAccuracy.spectrum.NullOrEmpty())
                StatDrawEntryAccuracy.spectrum = colors.Value.colorList.ListFullCopy();

            /*
            extremeCompatMode = Settings.GetHandle<bool>(
                    "princess.ExtraStats.extremeCompatMode",
                    "princess.ExtraStats.extremeCompatMode".Translate(),
                    "princess.ExtraStats.extremeCompatMode.Desc".Translate(),
                    false);*/
            }
        public override void SettingsChanged()
            {
            base.SettingsChanged();

            StatDrawEntryAccuracy.minLevel = minLevel;
            StatDrawEntryAccuracy.maxLevel = maxLevel;
            StatDrawEntryAccuracy.scale = scale;
            StatDrawEntryAccuracy.beyond = beyond;
            StatDrawEntryAccuracy.spectrum = colors.Value.colorList.ListFullCopy();
            // Clean up caches
            StatDrawEntryAccuracy.renderedStats.Clear();
            StatDrawEntryAccuracy.bar = null;
            StatDrawEntryAccuracy.barBeyond = null;
            }

        public class ColorArrayHandle : SettingHandleConvertible
            {
            public List<Color> colorList = new List<Color>();

            public override bool ShouldBeSaved => colorList.Count > 0 && !colorList.SetsEqual(StatDrawEntryAccuracy.defaultSpectrum);

            public override void FromString(string settingValue)
                {
                colorList = DirectXmlLoader.ItemFromXmlString<List<Color>>(settingValue, "([ExtraStats] internal color settings loader)");
                }

            public override string ToString()
                {
                return DirectXmlSaver.XElementFromObject(colorList, typeof(List<Color>), "List-Color").ToString();
                }

            public ColorArrayHandle initialize()
                {
                colorList = StatDrawEntryAccuracy.defaultSpectrum.ListFullCopy();
                return this;
                }
            private static Color WindowBGBorderColor = (Color)AccessTools.Field(typeof(Widgets), "WindowBGBorderColor").GetValue(null);

            public static readonly float ELEMENT_HEIGHT = 48f;
            public static readonly float ELEMENT_HEIGHT_QUARTER = ELEMENT_HEIGHT / 4f;
            public static readonly float ELEMENT_HEIGHT_MICRO = ELEMENT_HEIGHT / 3f;
            public static readonly float ELEMENT_MARGIN = 4f;
            public static readonly float ELEMENT_MARGIN_SLIDER = (ELEMENT_HEIGHT_MICRO - ELEMENT_HEIGHT_QUARTER) / 2;

            public static float textOffset;

            public static Vector2 scroll;

            public static readonly Regex REGEX = new Regex("^[1]?[0-9]{1,2}$|^[2][0-4][0-9]$|^[2][5][0-6]$");

            // i - button type
            // 0:^, 1:v, 2:+, 3:x
            // 64 elements ought to be enough for everybody
            public static bool[,] buttons = new bool[64, 4];

            public static bool customDrawer(Rect rect, ref List<Color> value)
                {
                //Widgets.DrawBoxSolidWithOutline(rect, Color.clear, Color.cyan);
                var font = Text.Font;
                var anchor = Text.Anchor;
                float curY = rect.y;

                float r, g, b;
                bool button;
                int? move = null;
                int? edit = null;

                bool change = false;

                Rect outRect = new Rect(rect);
                outRect.height = ELEMENT_HEIGHT * value.Count + ELEMENT_MARGIN * (value.Count - 1);
                outRect.width -= ELEMENT_HEIGHT;

                Widgets.BeginScrollView(rect, ref scroll, outRect);

                Rect innerRect = new Rect(outRect);
                innerRect.x += ELEMENT_HEIGHT + ELEMENT_MARGIN;
                innerRect.width -= ELEMENT_HEIGHT * 2 + ELEMENT_MARGIN;
                innerRect.height = ELEMENT_HEIGHT_MICRO;

                for (int i = 0; i < value.Count; i++)
                    {
                    Text.Anchor = TextAnchor.MiddleCenter;

                    Widgets.DrawBoxSolidWithOutline(new Rect(outRect.x, curY, ELEMENT_HEIGHT, ELEMENT_HEIGHT).ContractedBy(ELEMENT_MARGIN), value[i], WindowBGBorderColor);

                    Rect rRect = new Rect(innerRect)
                        {
                        y = curY
                        };
                    r = Widgets.HorizontalSlider(rRect.ContractedBy(0, ELEMENT_MARGIN_SLIDER), value[i].r.toColorInt(), 0, 255, roundTo: 1).toColorFloat();

                    rRect.x = rRect.xMax;
                    rRect.y -= textOffset;
                    rRect.width = ELEMENT_HEIGHT / 2 ;
                    Text.Font = GameFont.Tiny;
                    r = int.Parse(Widgets.TextField(rRect, r.toColorInt().ToString(), 3, REGEX)).toColorFloat();

                    if (i > 0)
                        {
                        rRect.x = rRect.xMax;
                        rRect.y = curY;
                        rRect.width = ELEMENT_HEIGHT_MICRO;
                        Text.Font = GameFont.Small;
                        button = Widgets.ButtonText(rRect, "▲");
                        if (!button && buttons[i, 0])
                            move = i;
                        buttons[i, 0] = button;
                        }
                    curY += ELEMENT_HEIGHT_MICRO;

                    Rect gRect = new Rect(innerRect)
                        {
                        y = curY
                        };
                    g = Widgets.HorizontalSlider(gRect.ContractedBy(0, ELEMENT_MARGIN_SLIDER), value[i].g.toColorInt(), 0, 255, roundTo: 1).toColorFloat();

                    gRect.x = gRect.xMax;
                    gRect.y -= textOffset;
                    gRect.width = ELEMENT_HEIGHT / 2;
                    Text.Font = GameFont.Tiny;
                    g = int.Parse(Widgets.TextField(gRect, g.toColorInt().ToString(), 3, REGEX)).toColorFloat();


                    gRect.x = gRect.xMax;
                    gRect.y = curY;
                    gRect.width = ELEMENT_HEIGHT_MICRO;
                    Text.Font = GameFont.Small;
                    button = Widgets.ButtonText(gRect, "✚");
                    if (!button && buttons[i, 2])
                        edit = i == 0? i : int.MaxValue;
                    buttons[i, 2] = button;

                    if (value.Count > 2)
                        {
                        gRect.x = gRect.xMax;
                        gRect.y = curY;
                        gRect.width = ELEMENT_HEIGHT_MICRO;
                        Text.Font = GameFont.Small;
                        button = Widgets.ButtonText(gRect, "✘");
                        if (!button && buttons[i, 3])
                            edit = -i;
                        buttons[i, 3] = button;
                        }

                    curY += ELEMENT_HEIGHT_MICRO;

                    Rect bRect = new Rect(innerRect)
                        {
                        y = curY
                        };
                    b = Widgets.HorizontalSlider(bRect.ContractedBy(0, ELEMENT_MARGIN_SLIDER), value[i].b.toColorInt(), 0, 255, roundTo: 1).toColorFloat();

                    bRect.x = bRect.xMax;
                    bRect.y -= textOffset;
                    bRect.width = ELEMENT_HEIGHT / 2;
                    Text.Font = GameFont.Tiny;
                    b = int.Parse(Widgets.TextField(bRect, b.toColorInt().ToString(), 3, REGEX)).toColorFloat();
                    //Widgets.Label(bRect, Mathf.Round(b * colorSpace).ToString());

                    if (i != value.Count - 1)
                        {
                        bRect.x = bRect.xMax;
                        bRect.y = curY;
                        bRect.width = ELEMENT_HEIGHT_MICRO;
                        Text.Font = GameFont.Small;
                        button = Widgets.ButtonText(bRect, "▼");
                        if (!button && buttons[i, 1])
                            move = -i;
                        buttons[i, 1] = button;
                        }
                    curY += ELEMENT_HEIGHT_MICRO;

                    if (Mathf.Abs(r - value[i].r) > Mathf.Epsilon || Mathf.Abs(g - value[i].g) > Mathf.Epsilon || Mathf.Abs(b - value[i].b) > Mathf.Epsilon)
                        change = true;

                    value[i] = new Color(r,g,b);
                    curY += ELEMENT_MARGIN;
                    }

                Widgets.EndScrollView();
                curY += ELEMENT_HEIGHT_MICRO;
                Text.Font = font;
                Text.Anchor = anchor;

                if (move is int mv) 
                    {
                    change = true;
                    bool direction = mv > 0;
                    mv = Math.Abs(mv);
                    Color storage = value[mv];
                    value[mv] = value[mv + (direction ? -1 : 1)];
                    value[mv + (direction ? -1 : 1)] = storage;
                    }
                else if (edit is int ed)
                    {
                    change = true;
                    bool isDel = ed <= 0;
                    if (ed == int.MaxValue) ed = 0;
                    else ed = Math.Abs(ed);
                    if (isDel)
                        value.RemoveAt(ed);
                    else value.Insert(ed, new Color(0, 0, 0));
                    }

                return change;
                }
            }
        }

    public static class SettingsExt
        {
        public static readonly float colorSpace = 255f;
        public static int toColorInt(this float c)
            {
            return Mathf.RoundToInt(c * colorSpace);
            }
        public static float toColorFloat(this int c)
            {
            return c / colorSpace;
            }
        public static float toColorFloat(this float c)
            {
            return c / colorSpace;
            }
        }

    }