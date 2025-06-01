using HarmonyLib;
using MelonLoader;
using MelonLoader.Utils;
using NeonLite;
using NeonLite.Modules;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TexturedDeck.Modules;
using UnityEngine;
using UnityEngine.Windows;
using UniverseLib.Runtime;
using static TexturedDeck.TexturedDeck;

namespace TexturedDeck
{
    public class TexturedDeck : MelonMod
    {
        internal static MelonLogger.Instance Logger { get; private set; }
        internal static Game Game { get { return Singleton<Game>.Instance; } }

        internal const string h = "TexturedDeck";

        internal static AssetBundle bundle;

        public override void OnInitializeMelon()
        {
            Logger = LoggerInstance;

            Settings.AddHolder(h);
            NeonLite.NeonLite.LoadModules(MelonAssembly);

            bundle = AssetBundle.LoadFromMemory(r.texdeck_bundle);
        }

        internal static void SaveAsPNG(Texture2D tex, string filename) => TextureHelper.SaveTextureAsPNG(tex, filename);
    }
}
