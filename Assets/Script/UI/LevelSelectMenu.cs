using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Деңгей таңдау терезесі. Карточкалар LevelList-тен (қанша LevelData болса, сонша) жасалады,
// ашық/жабық екенін және ең жақсы ұпайды save.json-нан алады.
// «Reset progress» → ойын ішіндегі растау терезесі (Yes / No).
public class LevelSelectMenu : MonoBehaviour
{
    [SerializeField] GameObject mainMenuPanel;
    [SerializeField] Transform cardRow;              // HorizontalLayoutGroup
    [SerializeField] Button cardTemplate;            // өшірулі үлгі, көшіріп аламыз
    [SerializeField] TextMeshProUGUI totalText;
    [SerializeField] GameObject confirmPanel;        // «Reset all progress?»

    readonly List<GameObject> cards = new List<GameObject>();

    public void Open()
    {
        mainMenuPanel.SetActive(false);
        gameObject.SetActive(true);
        confirmPanel.SetActive(false);
        Refresh();
    }

    public void Close()
    {
        confirmPanel.SetActive(false);
        gameObject.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    void Refresh()
    {
        foreach (GameObject c in cards) Destroy(c);
        cards.Clear();
        cardTemplate.gameObject.SetActive(false);

        LevelList list = LevelList.Instance;
        if (list == null) return;

        foreach (LevelData level in list.levels)
        {
            if (level == null) continue;
            Button card = Instantiate(cardTemplate, cardRow);
            card.gameObject.SetActive(true);
            card.name = "Card " + level.levelNumber;

            bool open = SaveSystem.IsUnlocked(level.levelNumber);
            int best = SaveSystem.BestScore(level.levelNumber);
            string status = !open ? "LOCKED" : best > 0 ? "Best " + best : "Not played";

            card.GetComponentInChildren<TextMeshProUGUI>(true).text =
                $"<size=150%><b>{level.levelNumber}</b></size>\n{level.displayName}\n<size=75%>{status}</size>";
            card.interactable = open;

            LevelData chosen = level;                // лямбда үшін жергілікті көшірме
            card.onClick.AddListener(() => GameSession.Play(chosen));
            cards.Add(card.gameObject);
        }

        totalText.text = $"Total best score: {SaveSystem.TotalBestScore}";
    }

    // ---------- Reset progress ----------

    public void AskReset() => confirmPanel.SetActive(true);

    public void CancelReset() => confirmPanel.SetActive(false);

    public void ConfirmReset()
    {
        SaveSystem.ResetProgress();                  // баптаулар қалады, прогресс нөлге түседі
        confirmPanel.SetActive(false);
        Refresh();
    }
}
