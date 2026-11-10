using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class Movement : MonoBehaviour
{
    // қозғалыс енді күшпен емес, жылдамдықпен беріледі
    public float speed = 6f;            // жүгіру жылдамдығы
    public float turnSpeed = 6f;        // бұрылу жылдамдығы (аз болса — баяу бұрылады)
    public float jumpImpulse = 5f;      // секіру (~1.27 м биіктік)
    public float airControl = 0.3f;     // ауадағы басқару (0..1)
    public float acceleration = 30f;    // жерде жылдамдық қаншалықты тез өзгереді (м/с²): аз болса — «ауыр» кейіпкер
    public float shootMoveMultiplier = 0.2f; // атып жатқанда жылдамдық (0.2 = қалыптының 20%-ы)
    public float jumpCooldown = 0.25f;  // екі секірудің арасы (жерден шыққанша қайта секірмесін)
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
    private float nextJumpTime;
    private bool gameOver = false;

    private Vector2 move;
    private bool fireRequested;
    private Shooter shooter;

    // ату: кейіпкер экран қараған жаққа бұрылады
    public float aimHoldTime = 0.6f;    // атқаннан кейін қанша секунд экран жаққа қарап тұрады
    private CameraFollow camFollow;
    private float aimUntil;
    private bool shootPending;
    private int aimLayer = -1;          // SwatAnimator-дағы AimLocomotion қабаты (атып жүру анимациясы)
    private float aimWeight;
    public float AimWeight => aimWeight;   // 0..1, атып жатқанда 1 (GunHold көздеу түзетуі үшін)          // бұрылған соң келесі кадрда атамыз (мылтық та бұрылып үлгереді)

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        shooter = GetComponent<Shooter>();
    }

    void Start()
    {
        cam = Camera.main.transform;    
        camFollow = cam.GetComponent<CameraFollow>();
        aimLayer = anim.GetLayerIndex("AimLocomotion");

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

        // жерде тұрса секіреді; бұрын velocity.y <= 0.1 тексерілетін —
        // рампада жоғары жүгіргенде y > 0 болып, секіру бөгелетін. Енді оның орнына кідіріс.
        if (grounded && Time.time >= nextJumpTime)
        {
            nextJumpTime = Time.time + jumpCooldown;
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
        UpdateAimAnimation(flat);

        // алдыңғы кадрда бұрылдық — енді мылтықтың ұшынан экран бағытына атамыз
        if (shootPending)
        {
            shootPending = false;
            if (shooter != null) shooter.Shoot(AimDirection());
        }

        // ату
        if (fireRequested)
        {
            fireRequested = false;

            bool fromFireButton = Gamepad.current != null
                                  && Gamepad.current.buttonWest.isPressed;

            // курсор бекітулі (компьютерде ойнап жатыр) — UI-ді баса алмайды, сол жақ батырма = ату
            bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;
            bool pointerOnUI = !cursorLocked
                               && EventSystem.current != null
                               && EventSystem.current.IsPointerOverGameObject();

            // экран қараған жаққа атады: алдымен кейіпкер бірден сол жаққа бұрылады
            if (shooter != null && (fromFireButton || !pointerOnUI))
            {
                Quaternion face = Quaternion.LookRotation(FlatAim());
                rb.rotation = face;
                transform.rotation = face;
                aimUntil = Time.time + aimHoldTime;
                shootPending = true;
            }
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

        // атып жатқанда баяу жүреді, ату тоқтаған соң қалпына келеді
        bool shooting = Time.time < aimUntil;
        Vector3 target = dir * speed * (shooting ? shootMoveMultiplier : 1f);

        // жерде: жылдамдық үдеумен өзгереді — WASD-ты жылдам кезек басса да бір орында дірілдемейді
        // ауада: бұрынғыдай — әр қадам сайын airControl үлесіне жақындайды (тез бұрылады)
        Vector3 current = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 horizontal = grounded
            ? Vector3.MoveTowards(current, target, acceleration * Time.fixedDeltaTime)
            : Vector3.Lerp(current, target, airControl);

        // y (гравитация, секіру) өзгермейді
        rb.linearVelocity = new Vector3(horizontal.x, rb.linearVelocity.y, horizontal.z);

        // атып жатқанда — экран жаққа қарап тұрады (жүгірсе де)
        if (shooting)
        {
            rb.MoveRotation(Quaternion.LookRotation(FlatAim()));
        }
        // әйтпесе кейіпкер жүрген жағына бұрылады
        else if (dir.sqrMagnitude > 0.01f)
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

    // атып жатқанда: жүгіру емес, экран жаққа қарап алға/артқа/жанына жүру анимациясы
    void UpdateAimAnimation(Vector3 flatVelocity)
    {
        if (aimLayer < 0) return;

        bool aiming = Time.time < aimUntil && grounded;
        aimWeight = Mathf.MoveTowards(aimWeight, aiming ? 1f : 0f, 8f * Time.deltaTime);
        anim.SetLayerWeight(aimLayer, aimWeight);

        // кейіпкерге қатысты жылдамдық: x — жанына, z — алға; 1 = атқандағы толық жылдамдық
        Vector3 local = transform.InverseTransformDirection(flatVelocity);
        float full = Mathf.Max(speed * shootMoveMultiplier, 0.1f);
        Vector2 m = Vector2.ClampMagnitude(new Vector2(local.x, local.z) / full, 1f);
        anim.SetFloat("MoveX", m.x, 0.1f, Time.deltaTime);
        anim.SetFloat("MoveZ", m.y, 0.1f, Time.deltaTime);
    }

    // экран қараған көлденең бағыт
    Vector3 FlatAim()
    {
        if (camFollow != null) return camFollow.FlatForward;
        Vector3 f = cam.forward; f.y = 0f;
        return f.sqrMagnitude > 0.001f ? f.normalized : transform.forward;
    }

    // оқтың бағыты: мылтықтың ұшынан экран ортасы (crosshair) көрсетіп тұрған нүктеге
    Vector3 AimDirection()
    {
        Vector3 from = (shooter != null && shooter.muzzle != null)
            ? shooter.muzzle.position
            : transform.position + Vector3.up * 1.4f;
        Vector3 to = camFollow != null ? camFollow.GetAimPoint() : from + cam.forward * 100f;

        Vector3 dir = to - from;
        // нысана мылтықтың артында қалса (қабырғаға тығылып тұрсаң) — жай камера бағытымен
        if (dir.sqrMagnitude < 0.01f || Vector3.Dot(dir.normalized, FlatAim()) < 0.1f)
            return cam.forward;
        return dir.normalized;
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