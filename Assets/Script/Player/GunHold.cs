using UnityEngine;

// Мылтықты кейіпкердің қолында ұстатады.
// Әр кадр анимациядан кейін (LateUpdate): тұтқасы оң қолда, ұңғысы сол қолға қарай бағытталады.
// Сондықтан idle, жүгіру, ату анимацияларының бәрінде мылтық қолдан шықпайды.
// Секіргенде қолдар мылтықтан ажыраса — мылтық кейіпкердің алдына қарайды.
//
// Көздеу түзетуі (aim correction): атып жатқанда кеуде сүйегі бұрылып, мылтықты экран ортасындағы
// нүктеге (crosshair) бағыттайды. Әр анимацияда дене әртүрлі еңкейеді (алға жүру, артқа жүру, strafe),
// бірақ түзетуден кейін мылтық әрқашан нысанаға қарайды.
public class GunHold : MonoBehaviour
{
    public Animator body;                    // бос болса — Movement.anim алынады
    public Vector3 gripOffset = new Vector3(0f, 0f, 0f);   // тұтқаның оң қолға қатысты ығысуы (әлем осьтерінде, мылтық бағытымен)
    public float minHandGap = 0.12f;         // қолдар бұдан жақын болса — бағыт сенімсіз
    public float maxHandGap = 0.7f;          // қолдар бұдан алыс болса — мылтықты ұстап тұрған жоқ
    public float turnSmooth = 25f;

    [Header("Көздеу түзетуі")]
    [Range(0f, 1f)] public float aimCorrection = 1f;   // 0 = өшірулі, 1 = мылтық дәл нысанаға қарайды
    public float maxCorrectionAngle = 50f;             // кеуде бұдан артық бұрылмайды

    Transform rightHand, leftHand, chest, root;
    Movement movement;
    CameraFollow follow;

    void Start()
    {
        movement = GetComponentInParent<Movement>();
        if (body == null && movement != null) body = movement.anim;
        if (body != null && body.isHuman)
        {
            rightHand = body.GetBoneTransform(HumanBodyBones.RightHand);
            leftHand = body.GetBoneTransform(HumanBodyBones.LeftHand);
            chest = body.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null) chest = body.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null) chest = body.GetBoneTransform(HumanBodyBones.Spine);
        }
        root = body != null ? body.transform : transform.parent;
        if (Camera.main != null) follow = Camera.main.GetComponent<CameraFollow>();
    }

    bool HandsOnGun(out Vector3 dir)
    {
        dir = root.forward;
        if (leftHand == null) return false;
        Vector3 toLeft = leftHand.position - rightHand.position;
        float gap = toLeft.magnitude;
        if (gap <= minHandGap || gap >= maxHandGap) return false;
        dir = toLeft / gap;
        return true;
    }

    void LateUpdate()
    {
        if (rightHand == null) return;

        AimCorrect();

        HandsOnGun(out Vector3 forward);
        Quaternion want = Quaternion.LookRotation(forward, root.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, want, turnSmooth * Time.deltaTime);
        transform.position = rightHand.position + transform.rotation * gripOffset;
    }

    // кеудені бұрып, мылтықтың бағытын crosshair нүктесіне келтіреді
    void AimCorrect()
    {
        if (chest == null || follow == null || movement == null || aimCorrection <= 0f) return;
        float w = movement.AimWeight * aimCorrection;
        if (w <= 0.01f) return;
        if (!HandsOnGun(out Vector3 gunDir)) return;

        Vector3 wantDir = (follow.GetAimPoint() - rightHand.position).normalized;
        Quaternion delta = Quaternion.FromToRotation(gunDir, wantDir);
        delta = Quaternion.RotateTowards(Quaternion.identity, delta, maxCorrectionAngle);   // шектеу
        delta = Quaternion.Slerp(Quaternion.identity, delta, w);                           // біртіндеп
        chest.rotation = delta * chest.rotation;   // қолдар мен мылтық кеудемен бірге бұрылады
    }
}
