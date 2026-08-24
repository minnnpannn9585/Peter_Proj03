using UnityEngine;
using UnityEngine.UI;

namespace ParcelSort
{
    public class DebugHud : MonoBehaviour
    {
        [SerializeField] Text label;

        YardDirector director;

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

            string phase = director.Phase == GamePhase.Prep ? UiStrings.PhasePrep : UiStrings.PhaseRunning;
            SpawnDirector spawner = director.Spawner;
            string queued = spawner != null ? "  (queued " + spawner.Queued + ")" : string.Empty;
            string text = director.LevelDisplayName + "   [" + phase + "]\n" +
                          UiStrings.PackagesLeft + "     " + director.PackagesLeft +
                          " / " + director.PackagesTotal + queued + "\n" +
                          UiStrings.Spawned + "  " + director.Spawned + "\n" +
                          UiStrings.Correct + "  " + director.Correct + "\n" +
                          UiStrings.Wrong + "    " + director.Wrong + "\n" +
                          UiStrings.Jams + "     " + director.Jams;

            InstallManager installer = director.Installer;
            if (installer != null)
            {
                text += "\n" + UiStrings.Gate + "     " + installer.InstalledCount + " installed";
            }

            label.text = text;
        }
    }
}
