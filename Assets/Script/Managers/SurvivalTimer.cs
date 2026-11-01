using UnityEngine;


public class SurvivalTimer : MonoBehaviour
{
    public float goal = 60f;           
    public Animator gateAnimator;      

    float t;
    bool won;
    float nextLog = 10f;

    void Update()
    {
        t += Time.deltaTime;           

        if (t >= nextLog && !won)       
        {
            Debug.Log("Survived: " + Mathf.FloorToInt(t) + " / " + goal + " s");
            nextLog += 10f;
        }

        if (!won && t >= goal)
        {
            won = true;
            Debug.Log("YOU WIN!");
            if (gateAnimator != null) gateAnimator.SetBool("Open", true);
        }
    }
}
