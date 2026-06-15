using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Configuración de Sistema")]
    public int targetFPS = 60;

    [Header("UI y Menús")]
    public GameObject pauseMenuUI;
    public GameObject LvlComplete;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI finalTimeText;
    public GameObject[] hudElements;

    public OptionsManager panelOpciones;    

    [Header("Estado del Juego")]
    private float elapsedTime = 0f;
    private bool isRunning    = true;
    public bool isPaused      = false;
    private bool gameEnded    = false;

    [Header("Navegación")]
    public int mainMenuSceneIndex = 0;

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);

        Application.targetFrameRate = targetFPS;
        QualitySettings.vSyncCount  = 0;
    }

    void Start()
    {
        if (panelOpciones != null) panelOpciones.gameObject.SetActive(false);

        if (SaveSystem.instance != null && SaveSystem.instance.isLoadingGame)
        {
            SaveData data = SaveSystem.instance.pendingLoad;
            if (data != null)
            {
                elapsedTime = data.elapsedTime;

                GameObject[] allEnemies = GameObject.FindGameObjectsWithTag("Damage");
                foreach (GameObject enemy in allEnemies)
                {
                    if (data.aliveEnemyIDs != null && !data.aliveEnemyIDs.Contains(enemy.name))
                        Destroy(enemy);
                }
            }

            SaveSystem.instance.isLoadingGame = false;
        }
    }

    void Update()
    {
        if (gameEnded) return;

        if (isRunning && !isPaused)
        {
            elapsedTime += Time.deltaTime;
            if (timerText != null)
                timerText.text = GetTimeString();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (panelOpciones != null && panelOpciones.gameObject.activeSelf)
            {
                CerrarOpciones();
                return;
            }

            if (isPaused) Resume();
            else Pause();
        }
    }

    public void ModificarTiempo(float cantidad)
    {
        elapsedTime += cantidad;
        
        //Evitamos que el tiempo sea negativo
        if (elapsedTime < 0f)
        {
            elapsedTime = 0f;
        }
    }

    public void Resume()
    {
        pauseMenuUI.SetActive(false);
        Time.timeScale   = 1f;
        isPaused         = false;
        Cursor.visible   = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void Pause()
    {
        pauseMenuUI.SetActive(true);
        Time.timeScale   = 0f;
        isPaused         = true;
        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void AbrirOpciones()
    {
        if (pauseMenuUI    != null) pauseMenuUI.SetActive(false);
        if (panelOpciones  != null) panelOpciones.OpenOptions();
    }

    public void CerrarOpciones()
    {
        if (panelOpciones  != null) panelOpciones.CloseOptions();
        if (pauseMenuUI    != null) pauseMenuUI.SetActive(true);
    }

    public void SaveAndQuit()
    {
        if (SaveSystem.instance == null)
        {
            Debug.LogError("❌ No se encontró SaveSystem en la escena.");
            return;
        }

        PlayerControllerComplete player = FindAnyObjectByType<PlayerControllerComplete>();

        GameObject[] enemiesInScene = GameObject.FindGameObjectsWithTag("Damage");
        List<string> aliveEnemies   = new List<string>();

        foreach (GameObject enemy in enemiesInScene)
            aliveEnemies.Add(enemy.name);

        if (player != null)
        {
            SaveSystem.instance.Save(
                sceneIndex     : SceneManager.GetActiveScene().buildIndex,
                playerPosition : player.transform.position,
                health         : player.CurrentHealth,
                time           : elapsedTime,
                enemies        : aliveEnemies
            );
        }
        else
        {
            SaveSystem.instance.Save(
                sceneIndex     : SceneManager.GetActiveScene().buildIndex,
                playerPosition : Vector3.zero,
                health         : 100,
                time           : elapsedTime,
                enemies        : aliveEnemies
            );
            Debug.LogWarning("⚠️ No se encontró el jugador al guardar. Se guardó con valores por defecto.");
        }

        GoToMainMenu();
    }

    public void GoToMainMenu()
    {
        Time.timeScale   = 1f;
        isPaused         = false;
        gameEnded        = false;
        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;
        SceneManager.LoadScene(mainMenuSceneIndex);
    }

    public void WinLevel()
    {
        if (gameEnded) return;

        gameEnded      = true;
        isRunning      = false;
        isPaused       = true;
        Time.timeScale = 0f;

        foreach (GameObject hud in hudElements)
            if (hud != null) hud.SetActive(false);

        if (LvlComplete != null)
        {
            LvlComplete.SetActive(true);
            LvlComplete.transform.SetAsLastSibling();
        }

        //Como usamos elapsedTime, el finalTimeText se actualizará con los segundos sumados/restados correctamente
        if (finalTimeText != null)
            finalTimeText.text = "Tiempo: " + GetTimeString();

        SaveSystem.instance?.DeleteSave();

        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;

        //Guarda el récord
    if (RecordSystem.instance != null)
    {
        int scene = SceneManager.GetActiveScene().buildIndex;
        string name = SceneManager.GetActiveScene().name;
        bool isRecord = RecordSystem.instance.TrySetRecord(scene, name, elapsedTime);
        if (isRecord) Debug.Log("¡Nuevo récord! " + GetTimeString());
    }

    }

    public string GetTimeString()
    {
        int minutes = Mathf.FloorToInt(elapsedTime / 60f);
        int seconds = Mathf.FloorToInt(elapsedTime % 60f);
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    public float ElapsedTime => elapsedTime;

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        isPaused       = false;
        gameEnded      = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        Debug.Log("Saliendo del juego...");
        Application.Quit();

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}