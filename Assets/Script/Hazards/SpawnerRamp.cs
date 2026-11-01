using System.Collections;
using UnityEngine;


public class SpawnerRamp : MonoBehaviour
{
    public Hazard[] prefabs;
    public float interval = 2f;
    public float rangeX = 8f, rangeZ = 8f, spawnHeight = 6f;
    public float rampEvery = 10f;     
    public float rampFactor = 0.9f;     
    public float minInterval = 0.4f;  

    IEnumerator Start()
    {
        yield return new WaitForSeconds(1f);
        float nextRamp = Time.time + rampEvery;

        while (true)
        {
            Hazard pick = prefabs[Random.Range(0, prefabs.Length)];
            Vector3 pos = new Vector3(
                transform.position.x + Random.Range(-rangeX, rangeX),
                spawnHeight,
                transform.position.z + Random.Range(-rangeZ, rangeZ));
            Instantiate(pick, pos, Quaternion.identity);

            if (Time.time >= nextRamp)
            {
                interval = Mathf.Max(minInterval, interval * rampFactor);
                nextRamp = Time.time + rampEvery;
                Debug.Log("Interval: " + interval);
            }
            yield return new WaitForSeconds(interval);
        }
    }
}
