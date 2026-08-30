using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ParcelSort
{
    /// <summary>Top right diagnostic readout. Kept from the original HUD, extended with the meta state.</summary>
    public class DebugHud : MonoBehaviour
    {
        [SerializeField] Text label;

        YardDirector director;
        readonly StringBuilder builder = new StringBuilder(256);

        public void Bind(YardDirector yardDirector, Text hudLabel)
        {
            director = yardDirector;
            if (hudLabel != null)
            {
                label = hudLabel;
            }
        }

        void Update()
        {
            if (director == null || label == null)
            {
                return;
            }

            builder.Clear();
            builder.Append(director.LevelDisplayName)
                .Append("   [").Append(UiStrings.PhaseName(director.Phase)).Append(']');
            if (director.IsPaused)
            {
                builder.Append("  ").Append(UiStrings.Paused);
            }

            builder.Append('\n');

            MissionDef selected = director.SelectedMission;
            if (selected != null)
            {
                builder.Append(selected.displayName);
                MissionRuntime mission = director.Mission;
                if (mission != null)
                {
                    builder.Append("   ").Append(mission.State);
                }

                builder.Append('\n');
            }

            SpawnDirector spawner = director.Spawner;
            builder.Append(UiStrings.PackagesLeft).Append("     ")
                .Append(director.PackagesLeft).Append(" / ").Append(director.PackagesTotal);
            if (spawner != null)
            {
                builder.Append("  (queued ").Append(spawner.Queued).Append(')');
            }

            builder.Append('\n')
                .Append(UiStrings.Spawned).Append("  ").Append(director.Spawned).Append('\n')
                .Append(UiStrings.Correct).Append("  ").Append(director.Correct).Append('\n')
                .Append(UiStrings.Wrong).Append("    ").Append(director.Wrong).Append('\n')
                .Append(UiStrings.Jams).Append("     ").Append(director.Jams).Append('\n');

            if (director.Wallet != null)
            {
                builder.Append(UiStrings.Coins).Append("    ").Append(director.Wallet.Balance).Append('\n');
            }

            InstallManager installer = director.Installer;
            if (installer != null)
            {
                builder.Append(UiStrings.Devices).Append("  ")
                    .Append(installer.InstalledCount).Append(" installed");

                DeviceInventory inventory = installer.Inventory;
                if (inventory != null)
                {
                    for (int i = 0; i < DeviceTypes.All.Length; i++)
                    {
                        DeviceType device = DeviceTypes.All[i];
                        if (inventory.Owned(device) == 0)
                        {
                            continue;
                        }

                        builder.Append("\n  ").Append(DeviceTypes.JsonKey(device)).Append(' ')
                            .Append(inventory.Installed(device)).Append('/')
                            .Append(inventory.Owned(device));
                    }
                }
            }

            label.text = builder.ToString();
        }
    }
}
