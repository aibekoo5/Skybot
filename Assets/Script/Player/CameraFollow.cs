using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

// Кейіпкерді айналып жүретін камера (third-person orbit).
// Компьютер: тінтуірді жылжытсаң бұрылады (курсор ойын кезінде жасырын, Esc → пауза → курсор шығады).
// Телефон: экранның бос жерін (батырма/джойстик емес) саусақпен сипасаң бұрылады.
// Movement камераның бағытымен жүреді, сондықтан «алға» әрқашан камера қараған жақ.
public class CameraFollow : MonoBehaviour
{
    public Transform target;

    [Header("Орны")]
    public float followDistance = 3.3f;  // кейіпкерден қашықтық
    public float pivotHeight = 1.5f;     // камера кейіпкердің қай биіктігіне қарайды
    public float defaultPitch = 8f;      // бастапқы еңкею (градус) — аз болса, экран ортасы алысқа қарайды
    public Vector3 shoulderOffset = new Vector3(0.6f, 0.25f, 0f); // камера иық үстінен қарайды: кейіпкер нысананы жаппайды
    public float minPitch = -10f;
    public float maxPitch = 60f;
    public float smooth = 12f;           // камераның ілесу жылдамдығы

    [Header("Бұру сезімталдығы")]
    public float mouseSensitivity = 0.15f;   // градус / пиксель
    public float touchSensitivity = 0.2f;    // градус / пиксель
    public bool invertY = false;

    [Header("Қабырға")]
    public float collisionRadius = 0.25f;    // камера қабырғаның ішіне кірмейді
    public LayerMask collisionMask = ~0;

    float yaw, pitch;
    Vector3 smoothPivot;
    float currentDist;
    int lookFinger = -1;                     // экранды бұрып тұрған саусақ
    readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    PointerEventData pointerData;

    [Header("Нысана")]
    public float aimDistance = 100f;         // экран ортасы ешнәрсеге тимесе — осы қашықтықтағы нүкте

    // экран қараған көлденең бағыт (кейіпкер осы жаққа бұрылады)
    public Vector3 FlatForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

    // экранның дәл ортасы (crosshair) әлемде қай нүктеге қарап тұр
    public Vector3 GetAimPoint(out Collider hitCollider)
    {
        hitCollider = null;
        Ray ray = new Ray(transform.position, transform.forward);
        // камера мен кейіпкердің арасындағы заттарды елемейміз
        float skip = Vector3.Dot(target.position + Vector3.up * pivotHeight - transform.position, transform.forward);
        Vector3 point = ray.GetPoint(aimDistance);
        float best = aimDistance;

        foreach (RaycastHit h in Physics.RaycastAll(ray, aimDistance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            if (h.transform.IsChildOf(target) || h.distance < skip) continue;
            if (h.distance < best)
            {
                best = h.distance;
                point = h.point;
                hitCollider = h.collider;
            }
        }
        return point;
    }

    public Vector3 GetAimPoint() => GetAimPoint(out _);

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
        SetCursorLocked(false);              // басқа сахнада (мәзір) курсор көрінуі керек
    }

    void Start()
    {
        yaw = target != null ? target.eulerAngles.y : transform.eulerAngles.y;
        pitch = defaultPitch;
        currentDist = followDistance;
        if (target != null) smoothPivot = target.position + Vector3.up * pivotHeight;
    }

    void LateUpdate()
    {
        if (target == null) return;

        bool paused = Time.timeScale == 0f;
        // тінтуір бар болса (компьютер) — оны тыңдаймыз; телефонда Mouse құрылғысы жоқ
        bool useMouse = Mouse.current != null && !Application.isMobilePlatform;

        if (useMouse) SetCursorLocked(!paused);
        if (!paused)
        {
            ReadTouch();
            if (useMouse && lookFinger == -1) ReadMouse();   // саусақпен бұрып жатса, тінтуір екі есе бұрмасын
        }

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        // кейіпкерге жұмсақ ілесеміз, ал бұрылу бірден — сонда кейіпкер дірілдемейді
        Vector3 pivot = target.position + Vector3.up * pivotHeight;
        smoothPivot = Vector3.Lerp(smoothPivot, pivot, smooth * Time.deltaTime);
        pivot = smoothPivot;

        // камераның керек орны: артта + иық үстінде
        Vector3 offset = rot * (shoulderOffset + Vector3.back * followDistance);
        float full = offset.magnitude;
        Vector3 dirOut = offset / full;

        // кейіпкер мен камераның арасында қабырға болса — камераны жақындатамыз
        float dist = full;
        foreach (RaycastHit h in Physics.SphereCastAll(pivot, collisionRadius, dirOut, full,
                                                       collisionMask, QueryTriggerInteraction.Ignore))
        {
            if (h.transform.IsChildOf(target)) continue;
            dist = Mathf.Min(dist, Mathf.Max(h.distance, 0.5f));
        }

        // қабырғаға тигенде бірден жақындайды, кейін баяу артқа шығады
        currentDist = dist < currentDist ? dist : Mathf.Lerp(currentDist, dist, 5f * Time.deltaTime);

        transform.position = pivot + dirOut * currentDist;
        transform.rotation = rot;
    }

    void ReadMouse()
    {
        if (Mouse.current == null) return;
        Vector2 d = Mouse.current.delta.ReadValue();
        yaw += d.x * mouseSensitivity;
        pitch += (invertY ? d.y : -d.y) * mouseSensitivity;
    }

    void ReadTouch()
    {
        foreach (Touch t in Touch.activeTouches)
        {
            int id = t.finger.index;

            // жаңа саусақ: батырма/джойстик үстінде болмаса — ол камераны бұрады
            if (t.began && lookFinger == -1 && !IsOverUI(t.screenPosition))
                lookFinger = id;

            if (id != lookFinger) continue;

            if (t.ended)
            {
                lookFinger = -1;
                continue;
            }

            Vector2 d = t.delta;
            yaw += d.x * touchSensitivity;
            pitch += (invertY ? d.y : -d.y) * touchSensitivity;
        }

        // саусақ жоғалып кетсе (мысалы, пауза кезінде)
        if (lookFinger != -1)
        {
            bool alive = false;
            foreach (Touch t in Touch.activeTouches)
                if (t.finger.index == lookFinger) alive = true;
            if (!alive) lookFinger = -1;
        }
    }

    bool IsOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        if (pointerData == null) pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = screenPos;
        uiHits.Clear();
        EventSystem.current.RaycastAll(pointerData, uiHits);
        return uiHits.Count > 0;
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
