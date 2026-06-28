using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class LevelRecord
{
    //campos publicos: LevelRecord es un DTO, no un MonoBehaviour
    public int sceneIndex;
    public float bestTime;
    public string levelName;
}

[System.Serializable]
public class AllRecords
{
    public List<LevelRecord> records = new List<LevelRecord>();
}

public class RecordSystem : MonoBehaviour
{
    public static RecordSystem instance;
    private const string PREF_KEY = "level_records";
    private AllRecords allRecords = new AllRecords();

    void Awake()
    {
        if (instance == null) { instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
        Load();
    }

    public bool TrySetRecord(int sceneIndex, string levelName, float time)
    {
        var record = allRecords.records.Find(r => r.sceneIndex == sceneIndex);
        if (record == null)
        {
            allRecords.records.Add(new LevelRecord { sceneIndex = sceneIndex,
                                                     levelName  = levelName,
                                                     bestTime   = time });
            Save();
            return true;
        }
        if (time < record.bestTime)
        {
            record.bestTime = time;
            Save();
            return true;
        }
        return false;
    }

    public LevelRecord GetRecord(int sceneIndex) =>
        allRecords.records.Find(r => r.sceneIndex == sceneIndex);

    public List<LevelRecord> GetAll() => allRecords.records;

    private void Save() =>
        PlayerPrefs.SetString(PREF_KEY, JsonUtility.ToJson(allRecords));

    private void Load()
    {
        string json = PlayerPrefs.GetString(PREF_KEY, "");
        if (!string.IsNullOrEmpty(json))
            allRecords = JsonUtility.FromJson<AllRecords>(json);
    }
}
