using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ParcelSort
{
    /// <summary>
    /// One device in the shop. Doubles as the buy button and the drag handle, so the whole loop
    /// of "buy it, then place it" happens on one card.
    ///
    /// The refusal feedback is deliberately specific: a maxed device greys out, an unaffordable
    /// one turns its price red and shakes, because "why can't I buy this" should never need a
    /// guess.
    /// </summary>
    public class DeviceShopCardUI : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        const float ShakeSeconds = 0.25f;
        const float ShakeAmplitude = 6f;

        Image background;
        Text nameLabel;
        Text priceLabel;
        Text ownedLabel;
        Text freeLabel;
        Button buyButton;
        Text buyLabel;
        RectTransform priceRect;

        DeviceShopBar bar;
        DeviceType device;
        bool dragging;
        float shakeRemaining;
        Vector2 priceHome;

        public DeviceType Device => device;

        public void Build(Transform parent)
        {
            var rect = gameObject.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = gameObject.AddComponent<RectTransform>();
            }

            rect.SetParent(parent, false);
            rect.sizeDelta = HudLayout.ShopCardSize;

            background = gameObject.AddComponent<Image>();
            background.color = HudFactory.CardColor;

            var element = gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = HudLayout.ShopCardSize.x;
            element.preferredHeight = HudLayout.ShopCardSize.y;
            element.minWidth = HudLayout.ShopCardSize.x;

            nameLabel = HudFactory.CreateText("Name", rect, HudFactory.Row(6f, 26f),
                string.Empty, 20, TextAnchor.UpperLeft, HudFactory.TextColor);
            priceLabel = HudFactory.CreateText("Price", rect, HudFactory.Row(34f, 24f),
                string.Empty, 18, TextAnchor.UpperLeft, HudFactory.FullColor);
            ownedLabel = HudFactory.CreateText("Owned", rect, HudFactory.Row(60f, 22f),
                string.Empty, 16, TextAnchor.UpperLeft, HudFactory.MutedTextColor);
            freeLabel = HudFactory.CreateText("Free", rect, HudFactory.Row(82f, 22f),
                string.Empty, 16, TextAnchor.UpperLeft, HudFactory.MutedTextColor);

            priceRect = priceLabel.rectTransform;
            priceHome = priceRect.anchoredPosition;

            buyButton = HudFactory.CreateButton("Buy", rect,
                new RectSpec(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
                    new Vector2(10f, 8f), new Vector2(-20f, 32f)),
                "购买", 18, out buyLabel);
            buyButton.onClick.AddListener(OnBuyClicked);
        }

        public void Bind(DeviceShopBar owner, DeviceType deviceType)
        {
            bar = owner;
            device = deviceType;
            if (nameLabel != null)
            {
                nameLabel.text = DeviceTypes.DisplayName(deviceType);
            }

            Refresh();
        }

        void OnBuyClicked()
        {
            if (bar == null || bar.Shop == null)
            {
                return;
            }

            if (bar.Shop.TryBuy(device))
            {
                // Owned and Free must be right on the same frame the purchase lands.
                bar.RefreshCards();
                return;
            }

            bar.Shop.CanBuy(device, out ShopError error);
            if (error == ShopError.NotEnoughCoins)
            {
                shakeRemaining = ShakeSeconds;
            }

            Refresh();
        }

        public void Refresh()
        {
            if (bar == null || bar.Shop == null)
            {
                return;
            }

            ShopService shop = bar.Shop;
            DeviceInventory inventory = shop.Inventory;

            int owned = inventory.Owned(device);
            int cap = shop.Cap(device);
            int free = inventory.Free(device);

            shop.CanBuy(device, out ShopError error);
            bool capped = error == ShopError.CapReached;
            bool broke = error == ShopError.NotEnoughCoins;

            if (priceLabel != null)
            {
                priceLabel.text = "价格 " + shop.Price(device);
                priceLabel.color = broke ? HudFactory.WarnColor : HudFactory.FullColor;
            }

            if (ownedLabel != null)
            {
                ownedLabel.text = "已有 " + owned + " / " + cap;
                ownedLabel.color = capped ? HudFactory.FullColor : HudFactory.MutedTextColor;
            }

            if (freeLabel != null)
            {
                freeLabel.text = "空闲 " + free;
                freeLabel.color = free > 0 ? HudFactory.GoodColor : HudFactory.MutedTextColor;
            }

            if (buyButton != null)
            {
                // Capped is a permanent no, so the button goes dead. An empty wallet is a
                // temporary no, so the button stays live and explains itself when pressed.
                buyButton.interactable = !capped;
                if (buyButton.targetGraphic != null)
                {
                    buyButton.targetGraphic.color = capped
                        ? HudFactory.CardDimColor
                        : HudFactory.CardColor;
                }
            }

            if (buyLabel != null)
            {
                buyLabel.text = capped ? "已满" : "购买";
            }

            if (background != null)
            {
                background.color = free > 0 ? HudFactory.CardColor : HudFactory.CardDimColor;
            }
        }

        void Update()
        {
            if (shakeRemaining <= 0f || priceRect == null)
            {
                return;
            }

            shakeRemaining -= Time.unscaledDeltaTime;
            if (shakeRemaining <= 0f)
            {
                priceRect.anchoredPosition = priceHome;
                return;
            }

            float offset = Mathf.Sin(shakeRemaining * 60f) * ShakeAmplitude *
                           (shakeRemaining / ShakeSeconds);
            priceRect.anchoredPosition = priceHome + new Vector2(offset, 0f);
        }

        // ------------------------------------------------------------ drag to install

        public void OnPointerClick(PointerEventData eventData)
        {
            // Clicks land on the buy button; this keeps the card itself from swallowing them.
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            InstallManager installer = Installer;
            dragging = installer != null && installer.Inventory.Free(device) > 0;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            Installer?.UpdateHover(eventData.position, device);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            InstallManager installer = Installer;
            if (installer == null)
            {
                return;
            }

            installer.UpdateHover(eventData.position, device);

            InstallSlotMarker marker = installer.HoveredMarker;
            NodeDeviceSlot nodeSlot = installer.HoveredNodeSlot;

            if (marker != null)
            {
                installer.TryInstall(device, marker);
            }
            else if (nodeSlot != null)
            {
                installer.TryInstall(device, nodeSlot);
            }

            installer.ClearHover();
            bar?.RefreshCards();
        }

        InstallManager Installer => bar != null && bar.Director != null
            ? bar.Director.Installer
            : null;
    }
}
