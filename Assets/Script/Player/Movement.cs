using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class Movement : MonoBehaviour
{
    // Барлық баланс сандары (жылдамдық, секіру, ...) — PlayerStats asset-те.
    // Таймер мен деңгей мақсаты — LevelData-да (LevelRunner басқарады).
    [SerializeField] PlayerStats stats;
    [SerializeField] ItemValues items;   // жауға соғылғандағы зақым

    // жерде тұрғанын тексеру
    public Transform feet;              // аяқтағы бос объект
    public float groundRadius = 0.2f;   // тексеру шарының радиусы
    public LayerMask groundMask;        // Ground layer

    public Animator anim;               // Hero ішіндегі Animator
    public Animator gateAnimator;

    public TextMeshProUGUI coinText;
    public TextMeshProUGUI winText;
    public TextMeshProUGUI timerText;   // LevelRunner жаңартады
    public AudioSource audioSource;
    public AudioClip coinSound;
    public AudioClip jumpSound;
public AudioClip jumpSound2;        // week 12: екінші клип — секіру қатарынан бірдей естілмейді
public AudioClip coinSound2;
    public AudioClip winSound;          // деңгей аяқталғанда

    public float messageTime = 2f;
    private Coroutine messageRoutine;
    private Rigidbody rb;
    private Transform cam;              // камера
    private int coinsCollected = 0;
    private int totalCoins;
    private bool grounded = false;      // onGround орнына
    private float nextJumpTime;
    private bool gameOver = false;

    private Vector2 move;
    private bool fireRequested;
    private Shooter shooter;

    // ату: кейіпкер экран қараған жаққа бұрылады
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
        if (winText != null) winText.text = "";   // Awake-те: LevelRunner.Start-тағы хабарды өшірмеу үшін
    }

    void Start()
    {
        cam = Camera.main.transform;    
        camFollow = cam.GetComponent<CameraFollow>();
        aimLayer = anim.GetLayerIndex("AimLocomotion");

        totalCoins = GameObject.FindGameObjectsWithTag("Coin").Length;
        Debug.Log("Coins in this level: " + totalCoins);

        if (stats == null) Debug.LogError("[Sky Bot] Movement: PlayerStats asset is not assigned", this);
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
            nextJumpTime = Time.time + stats.jumpCooldown;
            Vector3 v = rb.linearVelocity;
            v.y = 0f;                                   // әр секіру бірдей биіктікте
            rb.linearVelocity = v;
            rb.AddForce(Vector3.up * stats.jumpImpulse, ForceMode.Impulse);

            AudioManager.PlayVaried(audioSource, jumpSound, jumpSound2, 0.9f);
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
                aimUntil = Time.time + stats.aimHoldTime;
                shootPending = true;
            }
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
        Vector3 target = dir * stats.moveSpeed * (shooting ? stats.shootMoveMultiplier : 1f);

        // жерде: жылдамдық үдеумен өзгереді — WASD-ты жылдам кезек басса да бір орында дірілдемейді
        // ауада: бұрынғыдай — әр қадам сайын airControl үлесіне жақындайды (тез бұрылады)
        Vector3 current = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 horizontal = grounded
            ? Vector3.MoveTowards(current, target, stats.acceleration * Time.fixedDeltaTime)
            : Vector3.Lerp(current, target, stats.airControl);

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
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, look, stats.turnSpeed * Time.fixedDeltaTime));
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
            if (LevelRunner.Current != null) LevelRunner.Current.OnCoinCollected();

            AudioManager.PlayVaried(audioSource, coinSound, coinSound2);

            if (coinsCollected >= totalCoins && gateAnimator != null)
                gateAnimator.SetBool("Open", true);
        }

        if (other.CompareTag("Exit"))
        {
            // мақсатты (монета / барлық жау) LevelData бойынша LevelRunner тексереді
            LevelRunner runner = LevelRunner.Current;
            if (runner == null) return;
            if (runner.CanExit(coinsCollected >= totalCoins, out string reason))
                runner.Complete();                      // ұпай → save.json → келесі деңгей
            else
                ShowMessage(reason, messageTime);
        }
    }

    // Ground тегін тексеру алынды — оны енді CheckSphere жасайды
    void OnCollisionEnter(Collision other)
    {
        // жау енді бірден өлтірмейді — 1 HP алады (Health → жыпылықтау)
        if (other.gameObject.CompareTag("Enemy"))
        {
            Health h = GetComponent<Health>();
            if (h != null) h.TakeDamage(items.enemyContactDamage);
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
        float full = Mathf.Max(stats.moveSpeed * stats.shootMoveMultiplier, 0.1f);
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

    // ойыншы тоқтайды, экранның ортасында хабар (LevelRunner шақырады)
    public void EndGame(string message)
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

    public void PlayWinSound()
    {
        // AudioManager арқылы: келесі сахна жүктелсе де дыбыс үзілмейді
        if (AudioManager.Instance != null) AudioManager.Instance.PlayWin(winSound);
        else if (audioSource != null && winSound != null) audioSource.PlayOneShot(winSound);
    }

    void ShowCoins()
    {
        coinText.text = "Coins: " + coinsCollected + " / " + totalCoins;
    }

    // уақытша хабар (мысалы, деңгей атауы немесе «Collect all coins first!»)
    public void ShowMessage(string message, float seconds)
    {
        if (gameOver) return;
        if (messageRoutine != null) StopCoroutine(messageRoutine);
        messageRoutine = StartCoroutine(MessageRoutine(message, seconds));
    }

    System.Collections.IEnumerator MessageRoutine(string message, float seconds)
    {
        winText.text = message;
        yield return new WaitForSeconds(seconds);

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