using UnityEngine;
using System.IO;
using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    //campos publicos: SaveData es un DTO, no un MonoBehaviour
    public int sceneIndex;
    public float playerX;
    public float playerY;
    public int playerHealth;
    public float elapsedTime;
    public List<string> aliveEnemyIDs;
}

public class SaveSystem : MonoBehaviour
{
    public static SaveSystem instance;

    //datos pendientes de aplicar cuando cargue la escena del juego
    [HideInInspector] public SaveData pendingLoad = null;
    [HideInInspector] public bool     isLoadingGame = false;

    private string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    //comprueba si existe un archivo guardado
    public bool HasSaveFile()
    {
        return File.Exists(SavePath);
    }

    //guarda la partida
    public void Save(int sceneIndex, Vector3 playerPosition, int health, float time, List<string> enemies)
    {
        SaveData data = new SaveData
        {
            sceneIndex    = sceneIndex,
            playerX       = playerPosition.x,
            playerY       = playerPosition.y,
            playerHealth  = health,
            elapsedTime   = time,
            aliveEnemyIDs = enemies
        };

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log("Partida guardada en: " + SavePath);
    }

    //carga la partida guardada
    public SaveData Load()
    {
        if (!HasSaveFile())
        {
            Debug.LogWarning("No hay archivo de guardado.");
            return null;
        }

        string json = File.ReadAllText(SavePath);
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        Debug.Log("Partida cargada desde escena: " + data.sceneIndex);
        return data;
    }

    //borra la partida guardada (en disco Y en memoria).
    //Limpiar pendingLoad es crítico: si no se limpia, al entrar al siguiente nivel
    //PlayerController reutiliza las coordenadas guardadas del nivel anterior
    //y el jugador aparece en la posición incorrecta.
    public void DeleteSave()
    {
        pendingLoad = null;
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log("Archivo de guardado eliminado.");
        }
    }
}
