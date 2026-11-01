using UnityEngine;

// жау екі нүкте арасында ары-бері жүреді (Level 2)
// Enemy тегі + Collider керек — зақымды Movement береді
public class Patrol : MonoBehaviour
{
    public Vector3 offset = new Vector3(4f, 0f, 0f);   // бастапқы орыннан қаншалықты алыс барады
    public float speed = 2f;

    Vector3 pointA;
    Vector3 pointB;
    Vector3 target;

    void Start()
    {
        pointA = transform.position;
        pointB = transform.position + offset;
        target = pointB;
    }

    void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        // жүрген жағына қарайды
        Vector3 dir = target - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir);

        // нүктеге жетті — кері бұрылады
        if (Vector3.Distance(transform.position, target) < 0.05f)
            target = target == pointA ? pointB : pointA;
    }
}
