using MelonLoader;
using NeonLite;
using NeonLite.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TexturedDeck.Modules
{
    internal class UICardChanges : IModule
    {
        const bool priority = true;
        static bool active = true;

        static MelonPreferences_Entry<bool> showAmmo;

        internal static Texture2D defaultBG;
        internal static Texture2D defaultMask;
        internal static Texture2D defaultBack;
        internal static Texture2D defaultBackN;
        internal static Texture2D defaultRip;

        internal static readonly int _Noise = Shader.PropertyToID("_Noise");
        internal static readonly int _RipNoise = Shader.PropertyToID("_RipNoise");

        static void Setup()
        {
            var setting = Settings.Add(TexturedDeck.h, "", "uiCard", "Enable UICard Changes", null, true, true);
            showAmmo = Settings.Add(TexturedDeck.h, "", "showAmmo", "Show Ammo", null, false);
            active = setting.SetupForModule(Activate, (_, after) => after);
        }

        static void Activate(bool activate)
        {
            Patching.TogglePatch(activate, typeof(UICardAesthetics), "SetCard", NeverBaked, Patching.PatchTarget.Prefix);
            Patching.TogglePatch(activate, typeof(UICardAesthetics), "SetCard", Manager.AddOverrides, Patching.PatchTarget.Transpiler);
            Patching.TogglePatch(activate, typeof(UICardAesthetics), "SetCard", FakeBaked, Patching.PatchTarget.Postfix);
            Patching.TogglePatch(activate, typeof(UICardBakedGraphic), "SetCollectible", HideBaked, Patching.PatchTarget.Prefix);
            Patching.TogglePatch(activate, typeof(UICardAesthetics), "SetCardAmmo", HideAmmo, Patching.PatchTarget.Prefix);
            Patching.TogglePatch(activate, typeof(UICardAesthetics), "RetrieveDefaultTextures", ActuallyMarkRetrieved, Patching.PatchTarget.Prefix);

            Patching.TogglePatch(activate, typeof(UICard), "SetCard", SetBacking, Patching.PatchTarget.Postfix);

            active = activate;
        }

        static void NeverBaked(UICardAesthetics __instance, PlayerCard card, ref bool useBakedGraphic, ref bool __state)
        {
            if (useBakedGraphic && __instance.cardBakedGraphic)
                __state = true;
            useBakedGraphic = false;

            if (!card.data.cardBGTextureOverride)
                __instance.CardMat.SetTexture(UICard._IDTexture, Manager.GetOverride(defaultBG));
            if (!card.data.cardColorMaskTextureOverride)
                __instance.CardMat.SetTexture(UICard._IDColorMask, Manager.GetOverride(defaultMask));
            __instance.CardMat.SetTexture(_RipNoise, Manager.GetOverride(defaultRip));
        }
        static void FakeBaked(UICardAesthetics __instance, bool __state)
        {
            if (__state)
            {
                var baked = __instance.cardBakedGraphic.card_Ammo;
                Helpers.Method(typeof(UICardBakedGraphic), "SetActiveRenderer").Invoke(__instance.cardBakedGraphic, [baked]);
                __instance.cardBakedGraphic.transform.localPosition = new(0, 0, -0.001f); // prevent zfighting
            }
        }
        static void HideBaked(bool collectible, bool force, ref Renderer ____currentActiveRenderer, bool ____currentActiveRendererIsCollectible)
        {
            if (____currentActiveRenderer && (____currentActiveRendererIsCollectible != collectible || force))
                ____currentActiveRenderer.gameObject.SetActive(collectible);
        }

        static bool HideAmmo(PlayerCard card)
        {
            if (showAmmo.Value)
                return true;
            if (!RM.mechController)
                return false;
            var deck = RM.mechController.GetCurrentDeck();
            if (deck == null)
                return false;

            return !deck.Any(x => x == card);
        }

        static void ActuallyMarkRetrieved(ref bool ____defaultTexturesRetrieved) => ____defaultTexturesRetrieved = true;

        static void SetBacking(UICard __instance)
        {
            var mat = __instance.cardBG.GetComponent<MeshRenderer>().material;
            mat.mainTexture = Manager.GetOverride(defaultBack);
            mat.SetTexture(_Noise, Manager.GetOverride(defaultBackN));
        }
    }
}
