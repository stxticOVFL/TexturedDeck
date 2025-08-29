using HarmonyLib;
using MelonLoader;
using MelonLoader.Utils;
using NeonLite;
using NeonLite.Modules;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Instrumentation;
using System.Reflection;
using UnityEngine;
using TexturedDeck.Modules;


namespace TexturedDeck
{
    internal class Manager : IModule
    {
        const bool priority = false;
        const bool active = true;

        static MelonPreferences_Entry<string> category;
        internal static MelonPreferences_Entry<bool> src;

        internal static bool ready;

        static void Setup()
        {
            category = Settings.Add(TexturedDeck.h, "", "category", "Pack", "The selected pack to use.\nSelecting a pack that doesn't exist will create it with most textures you want.", "Default");
            category.SetupForModule(Activate, (_, _) => !Directory.Exists(PackPath));

            src = Settings.Add(TexturedDeck.h, "", "srcVerify", "SRC Verifiable", "If enabled, disables setting the BG and the mask for the card.", true);
            src.SetupForModule(Activate, (_, _) => !Directory.Exists(PackPath));

            //CrystalRenderer.SetupS(); // make sure these are after pack
        }

        static string CheckVerifiable()
        {
            if (!src.Value)
                return "SRC Verifiable setting is disabled.";
            return null;
        }

        static string PackPath => Path.Combine(MelonEnvironment.ModsDirectory, TexturedDeck.h, category.Value);

        internal abstract class TextureOverride
        {
            public Texture newTex;
            public List<TextureOverride> children = [];

            public bool dirty = true;

            public override string ToString() => $"{children.Count} children";

            public void SetDirty()
            {
                dirty = true;
                foreach (var child in children)
                    child.SetDirty();
            }

            public abstract bool CheckDirty();

            public virtual void FetchTexture()
            {
                foreach (var child in children)
                    child.FetchTexture();
            }
        }

        internal class FilenameOverride : TextureOverride
        {
            public string filename = null;
            DateTime lastUpdated = DateTime.MinValue;

            public FilenameOverride(int anisoLevel, FilterMode filter)
            {
                newTex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    anisoLevel = anisoLevel,
                    filterMode = filter
                };
            }

            public override string ToString() => $"{filename} | {children.Count} children";

            public override bool CheckDirty()
            {
                if (dirty || filename == null)
                    return dirty;
                if (!File.Exists(filename))
                    dirty = false;
                else
                {
                    var fileUpdated = File.GetLastWriteTimeUtc(filename);
                    if (fileUpdated != lastUpdated)
                    {
                        SetDirty();
                        lastUpdated = fileUpdated;
                    }
                }
                return dirty;
            }

            public override void FetchTexture()
            {
                base.FetchTexture();

                if (!dirty)
                    return;

                try
                {
                    var bytes = File.ReadAllBytes(filename);
                    ImageConversion.LoadImage(newTex as Texture2D, bytes, true);
                }
                catch
                {
                    return;
                }

                dirty = false;
            }
        }

        internal class CrystalOverride : TextureOverride
        {
            public PlayerCard card;
            const int RES = 512;

#if DEBUG
            Texture2D debug;
#endif
            public CrystalOverride(int anisoLevel, FilterMode filterMode, PlayerCardData card)
            {
                var rTex = new RenderTexture(RES, RES, 0, RenderTextureFormat.ARGB32)
                {
                    filterMode = filterMode,
                    anisoLevel = anisoLevel,
                    wrapModeU = TextureWrapMode.Repeat
                };
                rTex.Create();
                newTex = rTex;
                this.card = new()
                {
                    data = card
                };
                this.card.Initialize();

#if DEBUG
                debug = new(RES, RES, TextureFormat.RGBA32, false);
#endif
            }

            public override bool CheckDirty() => dirty;

            public override void FetchTexture()
            {
                base.FetchTexture();
                if (!dirty)
                    return;

                dirty = !CrystalRenderer.GetTexture(this);
#if DEBUG
                var active = RenderTexture.active;
                RenderTexture.active = newTex as RenderTexture;
                debug.ReadPixels(new Rect(0, 0, RES, RES), 0, 0);
                debug.Apply();
                RenderTexture.active = active;
#endif
            }
        }

        internal static readonly Dictionary<Texture2D, TextureOverride> overrides = [];

        internal static Texture2D GetOverride(Texture2D texture)
        {
            if (texture == null)
                return null;
            if (!overrides.TryGetValue(texture, out var ovride))
                return texture;
            if (ovride.newTex == null)
                return texture;
            return ovride.newTex as Texture2D;
        }

        internal static Texture2D GetOverrideSRC(Texture2D texture)
        {
            if (src.Value)
                return texture;
            return GetOverride(texture);
        }
        static readonly Dictionary<string, string> readableToID = new() {
            { "Katana",         "KATANA" },
            { "Fists",          "FISTS" },
            { "Miracle Katana", "KATANA_MIRACLE" },
            { "Purify",         "MACHINEGUN" },
            { "Elevate",        "PISTOL" },
            { "Godspeed",       "RIFLE" },
            { "Stomp",          "UZI" },
            { "Fireball",       "SHOTGUN" },
            { "Dominion",       "ROCKETLAUNCHER" },
            { "Book of Life",   "RAPTURE" },
            { "Health",         "HEALTH" },
            { "Ammo",           "AMMO"}
        };

        static Dictionary<string, string> idToReadable = null;

        static void Activate(bool export)
        {
            if (idToReadable == null)
            {
                idToReadable = [];
                foreach (var kv in readableToID)
                    idToReadable.Add(kv.Value, kv.Key);
            }

            var icon = Path.Combine(PackPath, "Designs");
            var detail = Path.Combine(PackPath, "Details");
            var gd = TexturedDeck.Game.GetGameData();

            if (export) // double check bc tricky
                export = !Directory.Exists(PackPath);

            IEnumerable<PlayerCardData> cards = [];
            if (export)
            {
                // dump the textures of the "common" cards and set the initial thingies

                Directory.CreateDirectory(icon);
                Directory.CreateDirectory(detail);

                cards = readableToID.Select(kv => gd.GetCard(kv.Value));
            }
            else
            {
                var files = Directory.GetFiles(icon, "*.png", SearchOption.TopDirectoryOnly);
                cards = files
                    .Select(Path.GetFileNameWithoutExtension)
                    .Select(x => gd.GetCard(
                        readableToID.TryGetValue(x, out var v) ? v : x))
                    .Where(x => x != null);
            }

            HashSet<TextureOverride> baseCrystals = [];
            HashSet<TextureOverride> allCrystals = [];

            string pathbuf;

            foreach (var card in cards)
            {
                TextureOverride crystalOverride = null;
                if (card.crystalTexture)
                {
                    if (!overrides.TryGetValue(card.crystalTexture, out crystalOverride))
                    {
                        crystalOverride = new CrystalOverride(card.crystalTexture.anisoLevel, card.crystalTexture.filterMode, card);
                        overrides.Add(card.crystalTexture, crystalOverride);
                    }
                    allCrystals.Add(crystalOverride);
                }

                void MakeOverride(Texture2D tex, bool skipPathbuf = false)
                {
                    if (!skipPathbuf)
                        pathbuf = Path.Combine(detail, $"{tex.name}.png");
                    if (!File.Exists(pathbuf))
                        TexturedDeck.SaveAsPNG(tex, pathbuf);

                    if (!overrides.TryGetValue(tex, out var texOverride))
                    {
                        texOverride = new FilenameOverride(tex.anisoLevel, tex.filterMode);
                        overrides.Add(tex, texOverride);
                    }
                    (texOverride as FilenameOverride).filename = pathbuf;

                    if (crystalOverride != null && !texOverride.children.Contains(crystalOverride))
                        texOverride.children.Add(crystalOverride);
                    texOverride.SetDirty();
                }

                var old = Path.Combine(icon, $"{card.cardID}.png");
                pathbuf = Path.Combine(icon, $"{idToReadable[card.cardID]}.png");

                if (File.Exists(old))
                    File.Move(old, pathbuf);
                MakeOverride(card.cardDesignTexture, true);

                if (!src.Value)
                {
                    if (card.cardBGTextureOverride)
                        MakeOverride(card.cardBGTextureOverride);
                    else if (card.crystalTexture)
                        baseCrystals.Add(crystalOverride);

                    if (card.cardColorMaskTextureOverride)
                        MakeOverride(card.cardColorMaskTextureOverride);
                    else if (card.crystalTexture)
                        baseCrystals.Add(crystalOverride);
                }
            }

            // default time
            var uiCard = Utils.CreateObjectFromResources("UICard", "UICard").GetComponent<UICard>();
            var uiCardA = uiCard.UICards[0];

            void AddDefault(Texture2D tex, string name, IEnumerable<TextureOverride> children, bool checkSrc = false)
            {
                if (checkSrc && src.Value)
                    return;

                pathbuf = Path.Combine(detail, $"{name}.png");
                if (!File.Exists(pathbuf))
                    TexturedDeck.SaveAsPNG(tex, pathbuf);

                if (!overrides.TryGetValue(tex, out var texOverride))
                {
                    texOverride = new FilenameOverride(tex.anisoLevel, tex.filterMode);
                    overrides.Add(tex, texOverride);
                }

                (texOverride as FilenameOverride).filename = pathbuf;

                texOverride.children.AddRange(children.Where(x => !texOverride.children.Contains(x)));
                texOverride.SetDirty();
            }

            var defaultBg = uiCardA.CardMat.GetTexture(UICard._IDTexture) as Texture2D;
            AddDefault(defaultBg, "Default BG", baseCrystals, true);
            UICardChanges.defaultBG = defaultBg;

            var defaultMask = uiCardA.CardMat.GetTexture(UICard._IDColorMask) as Texture2D;
            AddDefault(defaultMask, "Default Mask", baseCrystals, true);
            UICardChanges.defaultMask = defaultMask;

            var defaultRip = uiCardA.CardMat.GetTexture(UICardChanges._RipNoise) as Texture2D;
            AddDefault(defaultRip, "Rip Noise", []);
            UICardChanges.defaultRip = defaultRip;

            var backing = uiCard.cardBG.GetComponent<MeshRenderer>().material.mainTexture as Texture2D;
            AddDefault(backing, "Backing", allCrystals);
            UICardChanges.defaultBack = backing;

            var backingN = uiCard.cardBG.GetComponent<MeshRenderer>().material.GetTexture(UICardChanges._Noise) as Texture2D;
            AddDefault(backingN, "Backing Noise", []);
            UICardChanges.defaultBackN = backingN;

            GameObject.Destroy(uiCard.gameObject);

            if (!CrystalRenderer.i)
                CrystalRenderer.SetupRenderer();

            ready = true;
            OnLevelLoad(null);
        }

        static void OnLevelLoad(LevelData _)
        {
            if (!ready)
                return;
            CrystalRenderer.SetActive(true);
            foreach (var kv in overrides)
            {
                if (kv.Value.CheckDirty())
                    kv.Value.FetchTexture();
            }
            CrystalRenderer.SetActive(false);
        }

        static readonly FieldInfo[] overrideFields = [
            Helpers.Field(typeof(PlayerCardData), "cardDesignTexture"),
            Helpers.Field(typeof(PlayerCardData), "cardBGTextureOverride"),
            Helpers.Field(typeof(PlayerCardData), "cardColorMaskTextureOverride"),
            Helpers.Field(typeof(PlayerCardData), "crystalTexture")
        ];

        static readonly FieldInfo[] overrideSRC = [
            Helpers.Field(typeof(PlayerCardData), "cardBGTextureOverride"),
            Helpers.Field(typeof(PlayerCardData), "cardColorMaskTextureOverride"),
        ];

        internal static IEnumerable<CodeInstruction> AddOverrides(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions)
                .MatchForward(true, new CodeMatch(x => overrideFields.Any(f => x.LoadsField(f))))
                .Repeat(m =>
                {
                    var f = (FieldInfo)m.Instruction.operand;
                    var src = overrideSRC.Contains(f);
                    m.Advance(1)
                    .InsertAndAdvance(CodeInstruction.Call(typeof(Manager), src ? "GetOverrideSRC" : "GetOverride"));
                })
                .InstructionEnumeration();
        }

    }
}
