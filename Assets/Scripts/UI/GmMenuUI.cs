using UnityEngine;
using UnityEngine.UI;

namespace ParcelSort
{
    /// <summary>Debug panel: swap between level files or reset back to the prep state.</summary>
    public class GmMenuUI : MonoBehaviour
    {
        [SerializeField] YardDirector director;
        [SerializeField] Button map1Button;
        [SerializeField] Button map2Button;
        [SerializeField] Button resetButton;
        [SerializeField] string map1File = "level_01.json";
        [SerializeField] string map2File = "level_02.json";

        void Awake()
        {
            if (map1Button != null)
            {
                map1Button.onClick.AddListener(() => Load(map1File));
            }

            if (map2Button != null)
            {
                map2Button.onClick.AddListener(() => Load(map2File));
            }

            if (resetButton != null)
            {
                resetButton.onClick.AddListener(ResetRound);
            }
        }

        YardDirector Director
        {
            get
            {
                if (director == null)
                {
                    director = FindFirstObjectByType<YardDirector>();
                }

                return director;
            }
        }

        void Load(string fileName)
        {
            YardDirector target = Director;
            if (target != null)
            {
                target.LoadMap(fileName);
            }
        }

        void ResetRound()
        {
            YardDirector target = Director;
            if (target != null)
            {
                target.ResetToPrep();
            }
        }
    }
}
