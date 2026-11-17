using UnityEngine;

// Аспаннан заттар (тас, жүрек) түсіреді. Барлық сандары — LevelData-дағы HazardWave.
// Сахнаға қолмен қойылмайды: LevelRunner деңгей басталғанда жасап, Begin(wave) шақырады.
public class Spawner : MonoBehaviour
{
    HazardWave wave;

    public void Begin(HazardWave w)
    {
        wave = w;
        transform.position = w.center;
        if (w.hazards != null && w.hazards.Length > 0)
            InvokeRepeating(nameof(Spawn), w.firstDelay, w.interval);
    }

    void Spawn()
    {
        Hazard pick = wave.hazards[Random.Range(0, wave.hazards.Length)];
        if (pick == null) return;

        Vector3 pos = new Vector3(
            wave.center.x + Random.Range(-wave.halfSize.x, wave.halfSize.x),
            wave.spawnHeight,
            wave.center.z + Random.Range(-wave.halfSize.y, wave.halfSize.y));

        Instantiate(pick, pos, Quaternion.identity);
    }
}
