using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ParcelSort
{
    /// <summary>
    /// Bottom left shop tray, shown during prep only. Replaces the old PrepBarUI: cards are now
    /// driven by the level's device catalog and the player's wallet rather than by a per-round
    /// loadout allowance.
    /// </summary>
    public class DeviceShopBar : MonoBehaviour, IHudPanel
    {
        readonly List<DeviceShopCardUI> cards = new List<DeviceShopCardUI>();

        RectTransform root;
        Text emptyLabel;
        YardDirector director;

        public GameObject Panel => gameObject;

        public YardDirector Director => director;

        public ShopService Shop => director != null ? director.Shop : null;

        public void Build(Transform parent)
        {
            root = HudFactory.Configure(
                gameObject, parent, HudLayout.DeviceShopBar, HudFactory.PanelColor);

            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = HudLayout.ShopBarSpacing;
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            emptyLabel = HudFactory.CreateText("Empty", root, HudFactory.Stretch(),
                "本关卡没有可购买的装置", 20, TextAnchor.MiddleCenter, HudFactory.MutedTextColor);

            // Exempt from the layout group so it can sit centred over the whole bar.
            emptyLabel.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            HudFactory.SetActive(emptyLabel.gameObject, false);
        }

        public void Bind(YardDirector yardDirector)
        {
            director = yardDirector;
            RebuildCards();
        }

        void RebuildCards()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    Destroy(cards[i].gameObject);
                }
            }

            cards.Clear();

            if (director == null || root == null || Shop == null)
            {
                return;
            }

            // Iterate the enum, not the catalog, so shop order is stable and a device the level
            // omits is reported once rather than quietly missing.
            for (int i = 0; i < DeviceTypes.All.Length; i++)
            {
                DeviceType device = DeviceTypes.All[i];
                if (!Shop.IsSold(device))
                {
                    Debug.LogError("Level '" + (director.Config != null ? director.Config.id : "?") +
                                   "' does not sell device '" + DeviceTypes.JsonKey(device) +
                                   "'; its shop card was skipped.");
                    continue;
                }

                var go = new GameObject("ShopCard_" + DeviceTypes.JsonKey(device),
                    typeof(RectTransform));
                var card = go.AddComponent<DeviceShopCardUI>();
                card.Build(root);
                card.Bind(this, device);
                cards.Add(card);
            }

            if (emptyLabel != null)
            {
                HudFactory.SetActive(emptyLabel.gameObject, cards.Count == 0);
                emptyLabel.transform.SetAsLastSibling();
            }
        }

        public void RefreshCards()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i]?.Refresh();
            }
        }

        public void Refresh()
        {
            RefreshCards();
        }
    }
}
