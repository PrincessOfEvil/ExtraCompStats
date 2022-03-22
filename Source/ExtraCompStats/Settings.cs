using HarmonyLib;
using HugsLib;
using HugsLib.Settings;
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

        public override string ModIdentifier
            {
            get { return "princess.ExtraStats"; }
            }
        public override void DefsLoaded()
            {
            Settings.ContextMenuEntries = new[] {new ContextMenuEntry("princess.ExtraStats.dump".Translate(), () =>
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
                })};

            minLevel = Settings.GetHandle<int>(
                    "princess.ExtraStats.minLevel",
                    "princess.ExtraStats.minLevel".Translate(),
                    "princess.ExtraStats.minLevel.desc".Translate(),
                    -5,
                    Validators.IntRangeValidator(-5, 0));
            maxLevel = Settings.GetHandle<int>(
                    "princess.ExtraStats.maxLevel",
                    "princess.ExtraStats.maxLevel".Translate(),
                    "princess.ExtraStats.maxLevel.desc".Translate(),
                    30,
                    Validators.IntRangeValidator(1, 60));

            scale = Settings.GetHandle<int>(
                    "princess.ExtraStats.scale",
                    "princess.ExtraStats.scale".Translate(),
                    "princess.ExtraStats.scale.desc".Translate(),
                    12,
                    Validators.IntRangeValidator(1, 16));

            beyond = Settings.GetHandle<int>(
                    "princess.ExtraStats.beyond",
                    "princess.ExtraStats.beyond".Translate(),
                    "princess.ExtraStats.beyond.desc".Translate(),
                    2,
                    Validators.IntRangeValidator(2, 16));


            colors = Settings.GetHandle<ColorArrayHandle>(
                    "princess.ExtraStats.colors",
                    "princess.ExtraStats.colors".Translate(),
                    "princess.ExtraStats.colors.desc".Translate());
            colors.CustomDrawerHeight = ColorArrayHandle.elementHeight * 3.5f;
            if (colors.Value == null) colors.Value = new ColorArrayHandle().initialize();
            colors.CustomDrawer = rect =>
                {
                if (colors.Value == null) colors.Value = new ColorArrayHandle().initialize();
                return ColorArrayHandle.CustomDrawer(rect, ref colors.Value.colors);
                };
            if (StatDrawEntryAccuracy.spectrum.NullOrEmpty())
                StatDrawEntryAccuracy.spectrum = colors.Value.colors.ListFullCopy<Color>();
            }
        public override void SettingsChanged()
            {
            base.SettingsChanged();

            StatDrawEntryAccuracy.minLevel = minLevel;
            StatDrawEntryAccuracy.maxLevel = maxLevel;
            StatDrawEntryAccuracy.scale = scale;
            StatDrawEntryAccuracy.beyond = beyond;
            StatDrawEntryAccuracy.spectrum = colors.Value.colors.ListFullCopy<Color>();
            // Clean up caches
            StatDrawEntryAccuracy.renderedStats.Clear();
            StatDrawEntryAccuracy.bar = null;
            StatDrawEntryAccuracy.barBeyond = null;
            }

        public class ColorArrayHandle : SettingHandleConvertible
            {
            public List<Color> colors = new List<Color>();

            public override bool ShouldBeSaved
                {
                get => colors.Count > 0 && !colors.SetsEqual(StatDrawEntryAccuracy.defaultSpectrum);
                }

            public override void FromString(string settingValue)
                {
                colors = DirectXmlLoader.ItemFromXmlString<List<Color>>(settingValue, "([ExtraStats] internal color settings loader)");
                }

            public override string ToString()
                {
                return DirectXmlSaver.XElementFromObject(colors, typeof(List<Color>), "List-Color").ToString();
                }

            public ColorArrayHandle initialize()
                {
                colors = StatDrawEntryAccuracy.defaultSpectrum.ListFullCopy<Color>();
                return this;
                }
            private static Color WindowBGBorderColor = (Color)AccessTools.Field(typeof(Widgets), "WindowBGBorderColor").GetValue(null);

            public static readonly float elementHeight = 48f;
            public static readonly float elementHeightQuarter = elementHeight / 4f;
            public static readonly float elementHeightMicro = elementHeight / 3f;
            public static readonly float elementMargin = 4f;
            public static readonly float elementMarginSlider = (elementHeightMicro - elementHeightQuarter) / 2;

            public static float textOffset;

            public static Vector2 scroll;

            public static readonly Regex regex = new Regex("^[1]?[0-9]{1,2}$|^[2][0-4][0-9]$|^[2][5][0-6]$");

            // i - button type
            // 0:^, 1:v, 2:+, 3:x
            // 64 elements ought to be enough for everybody
            public static bool[,] buttons = new bool[64, 4];

            public static bool CustomDrawer(Rect rect, ref List<Color> value)
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
                outRect.height = elementHeight * value.Count + elementMargin * (value.Count - 1);
                outRect.width -= elementHeight;

                Widgets.BeginScrollView(rect, ref scroll, outRect);

                Rect innerRect = new Rect(outRect);
                innerRect.x += elementHeight + elementMargin;
                innerRect.width -= elementHeight * 2 + elementMargin;
                innerRect.height = elementHeightMicro;

                for (int i = 0; i < value.Count; i++)
                    {
                    Text.Anchor = TextAnchor.MiddleCenter;

                    Widgets.DrawBoxSolidWithOutline(new Rect(outRect.x, curY, elementHeight, elementHeight).ContractedBy(elementMargin), value[i], WindowBGBorderColor);

                    Rect rRect = new Rect(innerRect)
                        {
                        y = curY
                        };
                    r = Widgets.HorizontalSlider(rRect.ContractedBy(0, elementMarginSlider), value[i].r.toColorInt(), 0, 255, roundTo: 1).toColorFloat();

                    rRect.x = rRect.xMax;
                    rRect.y -= textOffset;
                    rRect.width = elementHeight / 2 ;
                    Text.Font = GameFont.Tiny;
                    r = int.Parse(Widgets.TextField(rRect, r.toColorInt().ToString(), 3, regex)).toColorFloat();

                    if (i > 0)
                        {
                        rRect.x = rRect.xMax;
                        rRect.y = curY;
                        rRect.width = elementHeightMicro;
                        Text.Font = GameFont.Small;
                        button = Widgets.ButtonText(rRect, "▲");
                        if (!button && buttons[i, 0])
                            move = i;
                        buttons[i, 0] = button;
                        }
                    curY += elementHeightMicro;

                    Rect gRect = new Rect(innerRect)
                        {
                        y = curY
                        };
                    g = Widgets.HorizontalSlider(gRect.ContractedBy(0, elementMarginSlider), value[i].g.toColorInt(), 0, 255, roundTo: 1).toColorFloat();

                    gRect.x = gRect.xMax;
                    gRect.y -= textOffset;
                    gRect.width = elementHeight / 2;
                    Text.Font = GameFont.Tiny;
                    g = int.Parse(Widgets.TextField(gRect, g.toColorInt().ToString(), 3, regex)).toColorFloat();


                    gRect.x = gRect.xMax;
                    gRect.y = curY;
                    gRect.width = elementHeightMicro;
                    Text.Font = GameFont.Small;
                    button = Widgets.ButtonText(gRect, "✚");
                    if (!button && buttons[i, 2])
                        edit = i == 0? i : int.MaxValue;
                    buttons[i, 2] = button;

                    if (value.Count > 2)
                        {
                        gRect.x = gRect.xMax;
                        gRect.y = curY;
                        gRect.width = elementHeightMicro;
                        Text.Font = GameFont.Small;
                        button = Widgets.ButtonText(gRect, "✘");
                        if (!button && buttons[i, 3])
                            edit = -i;
                        buttons[i, 3] = button;
                        }

                    curY += elementHeightMicro;

                    Rect bRect = new Rect(innerRect)
                        {
                        y = curY
                        };
                    b = Widgets.HorizontalSlider(bRect.ContractedBy(0, elementMarginSlider), value[i].b.toColorInt(), 0, 255, roundTo: 1).toColorFloat();

                    bRect.x = bRect.xMax;
                    bRect.y -= textOffset;
                    bRect.width = elementHeight / 2;
                    Text.Font = GameFont.Tiny;
                    b = int.Parse(Widgets.TextField(bRect, b.toColorInt().ToString(), 3, regex)).toColorFloat();
                    //Widgets.Label(bRect, Mathf.Round(b * colorSpace).ToString());

                    if (i != value.Count - 1)
                        {
                        bRect.x = bRect.xMax;
                        bRect.y = curY;
                        bRect.width = elementHeightMicro;
                        Text.Font = GameFont.Small;
                        button = Widgets.ButtonText(bRect, "▼");
                        if (!button && buttons[i, 1])
                            move = -i;
                        buttons[i, 1] = button;
                        }
                    curY += elementHeightMicro;

                    if (r != value[i].r || g != value[i].g || b != value[i].b)
                        change = true;

                    value[i] = new Color(r,g,b);
                    curY += elementMargin;
                    }

                Widgets.EndScrollView();
                curY += elementHeightMicro;
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