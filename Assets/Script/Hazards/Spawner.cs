using UnityEngine;


public class Spawner : MonoBehaviour
{
    public Hazard[] prefabs;            
    public float firstDelay = 1f;
    public float interval = 2f;

    public float rangeX = 8f;          
    public float rangeZ = 8f;          
    public float spawnHeight = 6f;     

    void Start()
    {

        InvokeRepeating(nameof(Spawn), firstDelay, interval);
    }

    void Spawn()
    {

        Hazard pick = prefabs[Random.Range(0, prefabs.Length)];

        Vector3 pos = new Vector3(
            transform.position.x + Random.Range(-rangeX, rangeX),
            spawnHeight,
            transform.position.z + Random.Range(-rangeZ, rangeZ));

        Instantiate(pick, pos, Quaternion.identity);
    }
}
