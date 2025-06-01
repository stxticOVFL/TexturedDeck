using MelonLoader;
using NeonLite;
using NeonLite.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace TexturedDeck.Modules
{
    internal class Miracle : IModule
    {
        const bool priority = false;
        const bool active = true;

        static readonly Dictionary<MiracleButton, UICard> cards = [];

        internal static MelonPreferences_Entry<bool> showText;

        static void Setup()
        {
            showText = Settings.Add(TexturedDeck.h, "", "miracleText", "Show Miracle Text", "Whether or not to display the card text in the Miracle menu.", true);
        }

        static void Activate(bool _)
        {
            Patching.AddPatch(typeof(MenuScreenMiracle), "OnSetVisible", MiracleVisible, Patching.PatchTarget.Postfix);
            Patching.AddPatch(typeof(MiracleButton), "ShowcaseCard", SetupUICard, Patching.PatchTarget.Postfix);
        }

        static void SetupUICard(MiracleButton __instance)
        {
            if (__instance.cardToShowcase.discardAbility == PlayerCardData.DiscardAbility.Back)
                return;
            if (!cards.TryGetValue(__instance, out UICard card))
            {
                card = Utils.CreateObjectFromResources("UICard", "UICard", __instance.transform).GetComponent<UICard>();
                cards.Add(__instance, card);

                card.transform.localPosition = new Vector2(0, -15);
                card.transform.localRotation = Quaternion.identity;
                card.transform.localScale = new Vector2(60, 60);
                card.SetTargetTransform(card.transform);
                card.ResetAesthetics();
                card.UICards[0].gameObject.SetActive(true);
                card.UICards[0].SetFakeAA(false);
                Helpers.Method(typeof(UICard), "ForceSpringTargets").Invoke(card, []);
                var pcard = new PlayerCard() { data = __instance.cardToShowcase };
                pcard.Initialize();
                card.SetCard(pcard);
                
                GameObject.Destroy(card.GetComponent<AudioObjectAmbience>());
                __instance.GetOrAddComponent<CanvasGroup>().alpha = 0;
                __instance.GetOrAddComponent<MiracleHelper>();
            }
            else
                card.SetCard(card.GetCurrentPlayerCard());
            card.UICards[0].textDiscardAbility_Localized.gameObject.SetActive(showText.Value);
            __instance.GetComponent<MiracleHelper>().SetAlpha(0);
        }

        static void MiracleVisible(MenuScreenMiracle __instance)
        {
            foreach (var button in __instance.buttonsToLoad)
            {
                var miracle = button.ButtonRef.GetComponent<MiracleButton>();
                miracle.ShowcaseCard(miracle.cardToShowcase);
            }
        }

        class MiracleHelper : MonoBehaviour
        {
            MiracleButton button;
            Image buttonImage;
            UICard card;

            Material backingMat;

            static readonly int _Opacity = Shader.PropertyToID("_Opacity");

            void Awake()
            {
                button = GetComponent<MiracleButton>();
                card = cards[button];
                buttonImage = GetComponent<Image>();
                backingMat = card.cardBG.GetComponent<MeshRenderer>().material;
            }

            internal void SetAlpha(float a)
            {
                var aesthetic = card.UICards[0];
                aesthetic.CardMat.SetFloat(UICard._IDDissolve, 1 - a);
                aesthetic.CardDesignMat.SetFloat(UICard._IDDissolve, AxKEasing.EaseOutCirc(0f, 1f, 1 - a));
                backingMat.SetFloat(_Opacity, a);
                if (card.GetCurrentCardData().discardAbility == PlayerCardData.DiscardAbility.Telefrag)
                    aesthetic.CardMat.SetFloat(UICard._IDActive, 0.5f + (Mathf.Sin(Time.unscaledTime * 3f) + 1f) * 0.75f);
            }

            void Update() => SetAlpha(buttonImage.color.a);
        }
    }
}
