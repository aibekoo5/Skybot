using UnityEngine;
using UnityEngine.SceneManagement;

// Win сахнасы: "You Win" + мәзірге қайту батырмасы
public class WinScreen : MonoBehaviour
{
    public void ToMenu()
    {
        SceneManager.LoadScene("Main menu");
    }

    public void PlayAgain()
    {
        GameSession.PlayFirst();   // LevelList-тегі бірінші деңгейден қайта
    }
}
