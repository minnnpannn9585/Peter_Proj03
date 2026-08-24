using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ParcelSort
{
    /// <summary>
    /// Bottom tray shown during the prep phase: one card per installable device plus the
    /// start button on the right. Hidden for the whole round once the player starts.
    /// </summary>
    public class PrepBarUI : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] Transform cardHolder;
        [SerializeField] GameObject cardPrefab;
        [SerializeField] Button startButton;
        [SerializeField] Text hintLabel;

        readonly List<DeviceCardUI> cards = new List<DeviceCardUI>();
        YardDirector director;

        public YardDirector Director => director;

        void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }

            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartClicked);
            }
        }

        void OnStartClicked()
        {
            if (director != null)
            {
                director.StartRound();
            }
        }

        public void Bind(YardDirector yardDirector)
        {
            director = yardDirector;
            RebuildCards();
            RefreshHint();
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

            if (director == null || cardPrefab == null || cardHolder == null)
            {
                return;
            }

            InstallManager installer = director.Installer;
            if (installer == null)
            {
                return;
            }

            foreach (DeviceType device in installer.Loadout.Devices)
            {
                GameObject go = Instantiate(cardPrefab, cardHolder);
                var card = go.GetComponent<DeviceCardUI>();
                if (card == null)
                {
                    card = go.AddComponent<DeviceCardUI>();
                }

                card.Bind(this, device);
                cards.Add(card);
            }

            // Keep the start button as the right-most element of the tray.
            if (startButton != null && startButton.transform.parent == cardHolder)
            {
                startButton.transform.SetAsLastSibling();
            }
        }

        public void Show(bool visible)
        {
            if (root != null)
            {
                root.SetActive(visible);
            }
        }

        public void RefreshCards()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    cards[i].Refresh();
                }
            }

            RefreshHint();
        }

        void RefreshHint()
        {
            if (hintLabel == null)
            {
                return;
            }

            if (director == null || director.Installer == null)
            {
                hintLabel.text = string.Empty;
                return;
            }

            int remaining = director.Installer.Loadout.Remaining(DeviceType.Gate);
            hintLabel.text = remaining > 0
                ? "Drag a gate onto a belt slot  (" + remaining + " left)"
                : "All devices placed. Press START.";
        }
    }
}
