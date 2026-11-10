using UnityEngine;
using TMPro;

// Outpost деңгейінің мақсаты: барлық жауды жою → шығыс қақпасы ашылады.
// Сахнадағы барлық EnemyBase-ті Start-та бір рет табады, олардың Died оқиғасын тыңдайды.
public class EnemyGate : MonoBehaviour
{
    [SerializeField] GameObject door;              // жабық қақпа (жойылғанда өшеді)
    [SerializeField] GameObject exitTrigger;       // Exit тегі бар триггер (басында өшірулі)
    [SerializeField] TextMeshProUGUI counterText;  // «Enemies: 3 / 6» (міндетті емес)
    [SerializeField] TextMeshProUGUI messageText;  // «Gate open!» хабары (міндетті емес)
    [SerializeField] float doorSinkSpeed = 2f;

    int total, alive;
    bool opening;

    void Start()
    {
        EnemyBase[] enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        total = alive = enemies.Length;
        foreach (EnemyBase e in enemies) e.Died += OnEnemyDied;
        if (exitTrigger != null) exitTrigger.SetActive(total == 0);
        UpdateText();
    }

    void OnEnemyDied(EnemyBase e)
    {
        alive--;
        UpdateText();
        if (alive <= 0) Open();
    }

    void Open()
    {
        opening = true;
        if (exitTrigger != null) exitTrigger.SetActive(true);
        if (messageText != null) messageText.text = "Gate open!";
        Invoke(nameof(ClearMessage), 2f);
    }

    void ClearMessage()
    {
        if (messageText != null && messageText.text == "Gate open!") messageText.text = "";
    }

    void Update()
    {
        // қақпа жерге батып кетеді
        if (!opening || door == null) return;
        door.transform.position += Vector3.down * doorSinkSpeed * Time.deltaTime;
        if (door.transform.position.y < -4f) door.SetActive(false);
    }

    void UpdateText()
    {
        if (counterText != null) counterText.text = $"Enemies: {total - alive} / {total}";
    }
}
