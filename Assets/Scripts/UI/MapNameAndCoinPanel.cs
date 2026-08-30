using UnityEngine;
using UnityEngine.UI;

namespace ParcelSort
{
    /// <summary>
    /// Top left panel: which yard the player is in and how much money they have. Visible in
    /// every phase, because the coin count is what every purchase decision is measured against.
    /// </summary>
    public class MapNameAndCoinPanel : MonoBehaviour, IHudPanel
    {
        [SerializeField] Text mapNameLabel;
        [SerializeField] Text coinLabel;

        YardDirector director;

        public GameObject Panel => gameObject;

        public void Build(Transform parent)
        {
            RectTransform root = HudFactory.Configure(
                gameObject, parent, HudLayout.MapNameAndCoinPanel, HudFactory.PanelColor);

            mapNameLabel = HudFactory.CreateText("MapNameLabel", root, HudLayout.MapNameLabel,
                string.Empty, 28, TextAnchor.UpperLeft, HudFactory.TextColor);

            // The coin readout sits immediately under the yard name so the two read as one block.
            coinLabel = HudFactory.CreateText("CoinLabel", root, HudLayout.CoinLabel,
                string.Empty, 24, TextAnchor.UpperLeft, HudFactory.FullColor);
        }

        public void Bind(YardDirector yardDirector)
        {
            director = yardDirector;
            Refresh();
        }

        /// <summary>
        /// Called from the wallet's change event rather than polled, so the number on screen and
        /// the balance in memory never disagree, even for one frame.
        /// </summary>
        public void Refresh()
        {
            if (director == null)
            {
                return;
            }

            if (mapNameLabel != null)
            {
                mapNameLabel.text = director.LevelDisplayName;
            }

            if (coinLabel != null)
            {
                int coins = director.Wallet != null ? director.Wallet.Balance : 0;
                coinLabel.text = "◎ " + coins;
            }
        }
    }
}
