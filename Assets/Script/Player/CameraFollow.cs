using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;         
    public float distance = 6f;      
    public float height = 3f;        
    public float turnSmooth = 3f;    
    public float smooth = 8f;        
    public float minSpeed = 1f;      
    private Rigidbody targetRb;
    private float yaw;               

    void Start()
    {
        yaw = transform.eulerAngles.y;
        if (target != null)
            targetRb = target.GetComponent<Rigidbody>();
    }

    void LateUpdate()
    {
        if (target == null || targetRb == null) return;

        Vector3 v = targetRb.linearVelocity;
        v.y = 0f;

        if (v.magnitude > minSpeed)
        {
            float moveYaw = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
            float diff = Mathf.DeltaAngle(yaw, moveYaw);

            if (Mathf.Abs(diff) < 120f)
            yaw = Mathf.MoveTowardsAngle(yaw, moveYaw, 45f * Time.deltaTime);       
             }

        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 wanted = target.position - rot * Vector3.forward * distance + Vector3.up * height;

        transform.position = Vector3.Lerp(transform.position, wanted, smooth * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * 1.2f);
    }
}