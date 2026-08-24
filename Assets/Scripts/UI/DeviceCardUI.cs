using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ParcelSort
{
    /// <summary>
    /// A draggable device card in the prep tray. Dragging highlights the nearest belt slot
    /// and dropping installs the device there, Plants-vs-Zombies style.
    /// </summary>
    public class DeviceCardUI : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] Text titleLabel;
        [SerializeField] Text countLabel;
        [SerializeField] Image background;

        static readonly Color ReadyColor = new Color(0.20f, 0.28f, 0.36f, 0.95f);
        static readonly Color EmptyColor = new Color(0.16f, 0.16f, 0.18f, 0.6f);

        PrepBarUI tray;
        DeviceType device;
        bool dragging;

        public void Bind(PrepBarUI owner, DeviceType deviceType)
        {
            tray = owner;
            device = deviceType;
            if (titleLabel != null)
            {
                titleLabel.text = deviceType.ToString().ToUpperInvariant();
            }

            Refresh();
        }

        public void Refresh()
        {
            int remaining = Remaining;
            if (countLabel != null)
            {
                countLabel.text = "x" + remaining;
            }

            if (background != null)
            {
                background.color = remaining > 0 ? ReadyColor : EmptyColor;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragging = Remaining > 0 && Installer != null;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            Installer.UpdateHover(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            InstallManager installer = Installer;
            InstallSlotMarker target = installer.UpdateHover(eventData.position);
            if (target != null)
            {
                installer.TryInstall(device, target);
            }

            installer.ClearHover();
            if (tray != null)
            {
                tray.RefreshCards();
            }
        }

        InstallManager Installer => tray != null && tray.Director != null
            ? tray.Director.Installer
            : null;

        int Remaining
        {
            get
            {
                InstallManager installer = tray != null && tray.Director != null
                    ? tray.Director.Installer
                    : null;
                return installer != null ? installer.Loadout.Remaining(device) : 0;
            }
        }
    }
}
