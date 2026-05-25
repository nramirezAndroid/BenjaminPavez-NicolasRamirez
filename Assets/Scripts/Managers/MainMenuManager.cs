using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("Paneles del Menú")]
    public GameObject panelMenuPrincipal;

    public GameObject panelConfirmacion;

    public OptionsManager panelOpciones;

    public GameObject panelScoreboard;

    [Header("Scoreboard")]
    public GameObject filaPrefab; 
    public Transform contentContenedor;

    [Header("Botones del Menú")]
    public Button nuevaPartidaButton;
    public Button continuarButton;
    public Button opcionesButton;   

    public Button salirButton;

    [Header("Botones de Confirmación")]
    public Button confirmSiButton;
    public Button confirmNoButton;

    [Header("Escenas")]
    public int primerNivelIndex = 1;
    void Start()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (SaveSystem.instance == null)
            Debug.LogError("❌ No hay SaveSystem en la escena.");

        ActualizarBotones();

        //Estado inicial de los paneles
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(true);
        if (panelConfirmacion  != null) panelConfirmacion.SetActive(false);
        if (panelOpciones      != null) panelOpciones.gameObject.SetActive(false);
        if (panelScoreboard    != null) panelScoreboard.SetActive(false);
    }

    public void NuevaPartida()
    {
        if (SaveSystem.instance == null || !SaveSystem.instance.HasSaveFile())
        {
            IniciarNuevaPartida();
            return;
        }

        //Si hay guardado se muestra la confirmación
        if (panelConfirmacion  != null) panelConfirmacion.SetActive(true);
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(false);
    }

    public void ConfirmarNuevaPartida()
    {
        IniciarNuevaPartida();
    }

    public void CancelarNuevaPartida()
    {
        if (panelConfirmacion  != null) panelConfirmacion.SetActive(false);
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(true);
    }

    private void IniciarNuevaPartida()
    {
        SaveSystem.instance?.DeleteSave();

        if (SaveSystem.instance != null)
        {
            SaveSystem.instance.pendingLoad   = null;
            SaveSystem.instance.isLoadingGame = false;
        }

        if (LoadingScreenManager.Instance != null)
        {
            LoadingScreenManager.Instance.LoadScene(primerNivelIndex);
        }
        else
        {
            SceneManager.LoadScene(primerNivelIndex);
        }
    }

    public void Continuar()
    {
        if (SaveSystem.instance == null) return;

        SaveData data = SaveSystem.instance.Load();
        if (data == null) return;

        SaveSystem.instance.pendingLoad   = data;
        SaveSystem.instance.isLoadingGame = true;

        if (LoadingScreenManager.Instance != null)
        {
            LoadingScreenManager.Instance.LoadScene(data.sceneIndex);
        }
        else
        {
            SceneManager.LoadScene(data.sceneIndex);
        }
    }

    public void AbrirOpciones()
    {
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(false);
        if (panelOpciones      != null) panelOpciones.OpenOptions();
    }

    public void CerrarOpciones()
    {
        if (panelOpciones      != null) panelOpciones.CloseOptions();
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(true);
    }
    public void Salir()
    {
        Debug.Log("Saliendo del juego...");
        Application.Quit();

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    private void ActualizarBotones()
    {
        bool haySave = SaveSystem.instance != null && SaveSystem.instance.HasSaveFile();

        if (continuarButton != null)
            continuarButton.gameObject.SetActive(haySave);
    }

    public void MostrarScoreboard()
    {
        
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(false);
        if (panelScoreboard != null) panelScoreboard.SetActive(true);
        // ----------------------------------------------

        //Limpia las filas antiguas para que no se dupliquen al abrir y cerrar el menú
        foreach (Transform child in contentContenedor)
        {
            Destroy(child.gameObject);
        }

        //Comprueba que el sistema de récords exista
        if (RecordSystem.instance == null) return;

        //Crea las filas en base a los datos guardados
        foreach (var record in RecordSystem.instance.GetAll())
        {
            //Clona el molde dentro del contenedor 'Content'
            GameObject nuevaFila = Instantiate(filaPrefab, contentContenedor);
        
            //Obtiene los dos componentes TextMeshPro de la fila clonada
            TextMeshProUGUI[] textos = nuevaFila.GetComponentsInChildren<TextMeshProUGUI>();
        
            if (textos.Length >= 2)
            {
                textos[0].text = record.levelName;
                int minutes = Mathf.FloorToInt(record.bestTime / 60f);
                int seconds = Mathf.FloorToInt(record.bestTime % 60f);
                textos[1].text = string.Format("{0:00}:{1:00}", minutes, seconds); 
            }
        }
    }
    public void CerrarScoreboard()
    {
        if (panelScoreboard != null) panelScoreboard.SetActive(false);
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(true);
    }
}