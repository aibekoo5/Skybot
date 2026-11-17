using UnityEngine;

// Level 2-дегі қарапайым патрульдік жау (екі нүкте арасында жүреді).
// Жүру аралығы (offset) — деңгейдің рельефі, ол сахнада қалады.
[CreateAssetMenu(fileName = "PatrollerStats", menuName = "Sky Bot/Enemy Stats/Patroller")]
public class PatrollerStats : ScriptableObject
{
    [Min(0f)] public float speed = 2.5f;                 // м/с
}
