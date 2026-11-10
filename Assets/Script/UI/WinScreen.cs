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
        SceneManager.LoadScene("Level2_Outpost");   // бірінші деңгейден қайта
    }
}
