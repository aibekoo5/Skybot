using UnityEngine;
using UnityEngine.UI;

// Canvas-қа қойылады. Экрандағы жүректерді басқарады.
public class HUD : MonoBehaviour
{
    [Header("Жүректер (сол жақ жоғарыда)")]
    public Image[] hearts;          // Heart1, Heart2, Heart3
    public Sprite fullHeart;        // толық жүрек суреті (міндетті емес)
    public Sprite emptyHeart;       // бос жүрек суреті (міндетті емес)

    // HP қанша болса, сонша жүрек көрсетеді
    public void SetHearts(int hp)
    {
        for (int i = 0; i < hearts.Length; i++)
        {
            if (hearts[i] == null) continue;

            bool full = i < hp;

            if (emptyHeart != null)
            {
                hearts[i].enabled = true;
                hearts[i].sprite = full ? fullHeart : emptyHeart;
            }
            else
            {
                hearts[i].enabled = full;
            }
        }
    }
}