using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class Movement : MonoBehaviour
{
    // қозғалыс енді күшпен емес, жылдамдықпен беріледі
    public float speed = 6f;            // жүгіру жылдамдығы
    public float turnSpeed = 12f;       // бұрылу жылдамдығы
    public float jumpImpulse = 6f;      // секіру
    public float airControl = 0.3f;     // ауадағы басқару (0..1)
    public float timeLimit = 60f;       // таймер

    // жерде тұрғанын тексеру
    public Transform feet;              // аяқтағы бос объект
    public float groundRadius = 0.2f;   // тексеру шарының радиусы
    public LayerMask groundMask;        // Ground layer

    public Animator anim;               // Hero ішіндегі Animator
    public Animator gateAnimator;

    public TextMeshProUGUI coinText;
    public TextMeshProUGUI winText;
    public TextMeshProUGUI timerText;
    public AudioSource audioSource;
    public AudioClip coinSound;
    public AudioClip jumpSound;
    public AudioClip winSound;          // деңгей аяқталғанда

    public float messageTime = 2f;
    private Coroutine messageRoutine;
    private Rigidbody rb;
    private Transform cam;              // камера
    private int coinsCollected = 0;
    private int totalCoins;
    private bool grounded = false;      // onGround орнына
    private float timeLeft;
    private bool gameOver = false;

    private Vector2 move;
    private bool fireRequested;
    private Shooter shooter;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        shooter = GetComponent<Shooter>();
    }

    void Start()
    {
        cam = Camera.main.transform;    

        totalCoins = GameObject.FindGameObjectsWithTag("Coin").Length;
        Debug.Log("Coins in this level: " + totalCoins);

        timeLeft = timeLimit;
        winText.text = "";
        ShowCoins();
    }

    // Player Input шақыратын әдістер 

    public void OnMove(InputValue value)
    {
        move = value.Get<Vector2>();    // бағытты енді FixedUpdate-те есептейміз
    }

    public void OnJump()
    {
        if (gameOver) return;

        // жерде тұрса және төмен құлап бара жатпаса ғана
        if (grounded && rb.linearVelocity.y <= 0.1f)
        {
            Vector3 v = rb.linearVelocity;
            v.y = 0f;                                   // әр секіру бірдей биіктікте
            rb.linearVelocity = v;
            rb.AddForce(Vector3.up * jumpImpulse, ForceMode.Impulse);

            if (audioSource != null && jumpSound != null)
                audioSource.PlayOneShot(jumpSound);
        }
    }

    public void OnFire()
    {
        fireRequested = true;
    }

    void Update()
    {
        if (gameOver) return;

        // жерде тұрмыз ба: аяқтың астындағы шар Ground layer-ге тие ме
        grounded = Physics.CheckSphere(feet.position, groundRadius, groundMask);

        // анимация параметрлері
        Vector3 flat = rb.linearVelocity;
        flat.y = 0f;
        anim.SetFloat("Speed", flat.magnitude);
        anim.SetBool("Grounded", grounded);

        // ату
        if (fireRequested)
        {
            fireRequested = false;

            bool fromFireButton = Gamepad.current != null
                                  && Gamepad.current.buttonWest.isPressed;

            bool pointerOnUI = EventSystem.current != null
                               && EventSystem.current.IsPointerOverGameObject();

            // кейіпкер қараған жаққа атады
            if (shooter != null && (fromFireButton || !pointerOnUI))
                shooter.Shoot(transform.forward);
        }

        // таймер (timeLimit = 0 болса — таймер жоқ, мысалы бастық деңгейінде)
        if (timeLimit > 0)
        {
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0)
            {
                timeLeft = 0;
                EndGame("Time's up!");
                Invoke(nameof(Restart), 2f);    // уақыт бітті — деңгей қайта басталады
            }
            if (timerText != null) timerText.text = "Time: " + Mathf.CeilToInt(timeLeft);
        }

        // аренадан құлау
        if (transform.position.y < -10f)
            Restart();
    }

    void FixedUpdate()
    {
        if (gameOver) return;

        // камераның «алға» және «оңға» бағыттары, тек жазықтықта (y = 0)
        Vector3 camF = cam.forward; camF.y = 0f; camF.Normalize();
        Vector3 camR = cam.right;   camR.y = 0f; camR.Normalize();

        // джойстик → әлемдегі бағыт
        Vector3 dir = camF * move.y + camR * move.x;

        // көлденең жылдамдық: жерде бірден, ауада баяу өзгереді
        Vector3 target = dir * speed;
        Vector3 current = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 horizontal = Vector3.Lerp(current, target, grounded ? 1f : airControl);

        // y (гравитация, секіру) өзгермейді
        rb.linearVelocity = new Vector3(horizontal.x, rb.linearVelocity.y, horizontal.z);

        // кейіпкер жүрген жағына бұрылады
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(dir);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, look, turnSpeed * Time.fixedDeltaTime));
        }
    }

    // өтіп кететін заттар: монета, шығыс
    void OnTriggerEnter(Collider other)
    {
        if (gameOver) return;

        if (other.CompareTag("Coin"))
        {
            Destroy(other.gameObject);
            coinsCollected++;
            ShowCoins();

            if (audioSource != null && coinSound != null)
                audioSource.PlayOneShot(coinSound);

            if (coinsCollected >= totalCoins && gateAnimator != null)
                gateAnimator.SetBool("Open", true);
        }

        if (other.CompareTag("Exit"))
        {
            if (coinsCollected >= totalCoins)
            {
                EndGame("Level complete!");
                if (audioSource != null && winSound != null)
                    audioSource.PlayOneShot(winSound);
                Invoke(nameof(NextLevel), 2f);  // 2 секундтан соң келесі деңгей
            }
            else
            {
                if (messageRoutine != null) StopCoroutine(messageRoutine);
                messageRoutine = StartCoroutine(ShowMessage("Collect all coins first!"));
            }
        }
    }

    // Ground тегін тексеру алынды — оны енді CheckSphere жасайды
    void OnCollisionEnter(Collision other)
    {
        // жау енді бірден өлтірмейді — 1 HP алады (Health → жыпылықтау)
        if (other.gameObject.CompareTag("Enemy"))
        {
            Health h = GetComponent<Health>();
            if (h != null) h.TakeDamage(1);
            else Restart();
        }
    }

    void EndGame(string message)
    {
        gameOver = true;
        winText.text = message;
        rb.linearVelocity = Vector3.zero;
        anim.SetFloat("Speed", 0f);     // тоқтағанда Idle-ге өтеді
    }

    void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Build Settings ретімен: Level1 → Level 2 → Level3_Boss
    void NextLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    void ShowCoins()
    {
        coinText.text = "Coins: " + coinsCollected + " / " + totalCoins;
    }

    System.Collections.IEnumerator ShowMessage(string message)
    {
        winText.text = message;
        yield return new WaitForSeconds(messageTime);

        if (!gameOver)
            winText.text = "";
    }

    public void GoToMenu()
    {
        SceneManager.LoadScene("Main menu");
    }

    // Scene терезесінде аяқтағы тексеру шарын көрсетеді (тек көмек үшін)
    void OnDrawGizmosSelected()
    {
        if (feet == null) return;
        Gizmos.color = grounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(feet.position, groundRadius);
    }
}