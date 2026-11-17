using UnityEngine;

// Деңгейлердің реті. Assets/Resources/LevelList.asset — кодтан Resources.Load арқылы табылады.
// Жаңа деңгей қосу = жаңа LevelData asset жасап, осы тізімге қосу.
[CreateAssetMenu(fileName = "LevelList", menuName = "Sky Bot/Level List")]
public class LevelList : ScriptableObject
{
    public LevelData[] levels;

    static LevelList instance;
    public static LevelList Instance
    {
        get
        {
            if (instance == null) instance = Resources.Load<LevelList>("LevelList");
            if (instance == null) Debug.LogError("[Sky Bot] Assets/Resources/LevelList.asset not found");
            return instance;
        }
    }

    public LevelData First => levels != null && levels.Length > 0 ? levels[0] : null;

    public LevelData FindByScene(string sceneName)
    {
        foreach (LevelData l in levels) if (l != null && l.sceneName == sceneName) return l;
        return null;
    }

    // тізімдегі келесі деңгей, соңғысы болса — null
    public LevelData Next(LevelData current)
    {
        int i = System.Array.IndexOf(levels, current);
        return i >= 0 && i + 1 < levels.Length ? levels[i + 1] : null;
    }
}
