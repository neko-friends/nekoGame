
using UnityEngine;
using UnityEngine.UI;

public class TerritoryUI : MonoBehaviour
{
    [Header("êwínä«óù")]
    [SerializeField] private TerritoryManager territoryManager;

    [Header("7ãÊâÊÇÃImage")]
    [SerializeField] private Image[] sectionImages = new Image[7];

    [Header("êwínâÊëú")]
    [SerializeField] private Sprite myTerritorySprite;
    [SerializeField] private Sprite opponentTerritorySprite;
    [SerializeField] private Sprite mixedTerritorySprite;
    [SerializeField] private Sprite neutralTerritorySprite;

    private void Start()
    {
        RefreshTerritoryUI();
    }

    public void RefreshTerritoryUI()
    {
        if (territoryManager == null)
            return;

        for (int i = 0; i < sectionImages.Length; i++)
        {
            if (sectionImages[i] == null)
                continue;

            TerritoryOwner owner =
                territoryManager.GetSectionOwner(i);

            switch (owner)
            {
                case TerritoryOwner.Left:
                    sectionImages[i].sprite =
                        myTerritorySprite;
                    break;

                case TerritoryOwner.Right:
                    sectionImages[i].sprite =
                        opponentTerritorySprite;
                    break;

                case TerritoryOwner.Mixed:
                    sectionImages[i].sprite =
                        mixedTerritorySprite;
                    break;

                case TerritoryOwner.Neutral:
                    sectionImages[i].sprite =
                        neutralTerritorySprite;
                    break;
            }
        }
    }
}

