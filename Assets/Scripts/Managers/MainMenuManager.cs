using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class MainMenuManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // PANELES PRINCIPALES
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Paneles del Menú")]
    public GameObject panelMenuPrincipal;
    public GameObject panelConfirmacion;
    public OptionsManager panelOpciones;
    public GameObject panelScoreboard;
    
    [Header("Panel de Red (Multijugador)")]
    public GameObject panelNetwork; 

    [Header("Sub-Paneles de Red")]
    public GameObject panelBotonesMultiplayer;
    
    public GameObject panelHostWait;      //Pantalla para mostrar la IP al Host
    public TextMeshProUGUI textoIPHost;   //Texto donde mostraremos su IP
    
    public GameObject panelClientJoin;    //Pantalla para que el Cliente escriba
    public TMP_InputField inputIPCliente; //Campo donde el cliente escribe la IP

    // ─────────────────────────────────────────────────────────────────────────
    // PANEL DE RED (Cooperativo Online)
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Panel de Red (Cooperativo)")]
    public GameObject panelNetwork;

    [Header("Sub-paneles de Red")]
    public GameObject panelBotonesMultiplayer;

    public GameObject panelHostWait;           //Muestra el CÓDIGO de sala al Host
    public TextMeshProUGUI textoIPHost;        
    public Button botonContinuarHost;          //Aparece tras crear la sala; carga el nivel

    public GameObject panelClientJoin;         //El cliente escribe el código
    public TMP_InputField inputIPCliente;      

    [Header("Estado de Conexión")]
    [Tooltip("Texto opcional para mostrar 'Conectando...' / errores")]
    public TextMeshProUGUI textoEstadoConexion;
    public GameObject loadingSpinner; 

    // ─────────────────────────────────────────────────────────────────────────
    // SCOREBOARD
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Scoreboard")]
    public GameObject filaPrefab;
    public Transform contentContenedor;

    // ─────────────────────────────────────────────────────────────────────────
    // BOTONES
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Botones del Menú")]
    public Button nuevaPartidaButton;
    public Button continuarButton;
    public Button cooperativoButton;
    public Button opcionesButton;
    public Button salirButton;

    [Header("Botones de Confirmación")]
    public Button confirmSiButton;
    public Button confirmNoButton;

    // ─────────────────────────────────────────────────────────────────────────
    // ESCENAS
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Escenas")]
    public int primerNivelIndex = 1;

    // ─────────────────────────────────────────────────────────────────────────
    // INICIO
    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        Time.timeScale   = 1f;
        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;

        if (SaveSystem.instance == null)
            Debug.LogError("❌ No hay SaveSystem en la escena.");

        ActualizarBotones();

        if (panelMenuPrincipal      != null) panelMenuPrincipal.SetActive(true);
        if (panelConfirmacion       != null) panelConfirmacion.SetActive(false);
        if (panelOpciones           != null) panelOpciones.gameObject.SetActive(false);
        if (panelScoreboard         != null) panelScoreboard.SetActive(false);
        if (panelNetwork            != null) panelNetwork.SetActive(false);
        if (panelBotonesMultiplayer != null) panelBotonesMultiplayer.SetActive(false);
        if (panelHostWait           != null) panelHostWait.SetActive(false);
        if (panelClientJoin         != null) panelClientJoin.SetActive(false);
        if (loadingSpinner          != null) loadingSpinner.SetActive(false);

        LimpiarEstadoConexion();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // NUEVA PARTIDA (Solitario)
    // ─────────────────────────────────────────────────────────────────────────

    public void NuevaPartida()
    {
        if (SaveSystem.instance == null || !SaveSystem.instance.HasSaveFile())
        {
            IniciarNuevaPartida();
            return;
        }

        if (panelConfirmacion  != null) panelConfirmacion.SetActive(true);
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(false);
    }

    public void ConfirmarNuevaPartida()
    {
        if (panelConfirmacion != null) panelConfirmacion.SetActive(false);
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

        NetworkModeData.modoSeleccionado = NetworkModeData.Mode.Solitario;
        EjecutarCargaDeNivel(primerNivelIndex);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CONTINUAR
    // ─────────────────────────────────────────────────────────────────────────

    public void Continuar()
    {
        if (SaveSystem.instance == null) return;

        SaveData data = SaveSystem.instance.Load();
        if (data == null) return;

        SaveSystem.instance.pendingLoad   = data;
        SaveSystem.instance.isLoadingGame = true;

        NetworkModeData.modoSeleccionado = NetworkModeData.Mode.Solitario;
        EjecutarCargaDeNivel(data.sceneIndex);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // COOPERATIVO ONLINE 
    // ─────────────────────────────────────────────────────────────────────────

    public void AbrirPanelCooperativo()
    {
        if (panelMenuPrincipal      != null) panelMenuPrincipal.SetActive(false);
        if (panelNetwork            != null) panelNetwork.SetActive(true);
        if (panelBotonesMultiplayer != null) panelBotonesMultiplayer.SetActive(true);
        if (panelHostWait           != null) panelHostWait.SetActive(false);
        if (panelClientJoin         != null) panelClientJoin.SetActive(false);
        LimpiarEstadoConexion();
    }

    public void CerrarPanelCooperativo()
    {
        if (panelNetwork            != null) panelNetwork.SetActive(false);
        if (panelBotonesMultiplayer != null) panelBotonesMultiplayer.SetActive(false);
        if (panelHostWait           != null) panelHostWait.SetActive(false);
        if (panelClientJoin         != null) panelClientJoin.SetActive(false);
        if (panelMenuPrincipal      != null) panelMenuPrincipal.SetActive(true);
    }

    public void VolverSeleccionCooperativo()
    {
        if (panelHostWait           != null) panelHostWait.SetActive(false);
        if (panelClientJoin         != null) panelClientJoin.SetActive(false);
        if (panelBotonesMultiplayer != null) panelBotonesMultiplayer.SetActive(true);
        LimpiarEstadoConexion();
    }


    public void AbrirPantallaHost()
    {
        if (panelBotonesMultiplayer != null) panelBotonesMultiplayer.SetActive(false);
        if (panelHostWait           != null) panelHostWait.SetActive(true);

        if (textoIPHost != null) textoIPHost.text = "";
        if (botonContinuarHost != null) botonContinuarHost.gameObject.SetActive(false);
        LimpiarEstadoConexion();
    }


    public async void ConfirmarHost()
    {
        MostrarEstadoConexion("Creando sala...");

        string joinCode = await RelayManager.instance.CreateRelayHost();

        if (string.IsNullOrEmpty(joinCode))
        {
            MostrarEstadoConexion("❌ No se pudo crear la sala. Revisa tu conexión a internet.");
            if (textoIPHost != null) textoIPHost.text = "";
            return;
        }

        if (textoIPHost != null)
            textoIPHost.text = $"Código de sala: {joinCode}";

        MostrarEstadoConexion("Comparte el código con tu compañero y presiona Continuar.");

        if (botonContinuarHost != null) botonContinuarHost.gameObject.SetActive(true);
    }

 
    public void CopiarCodigoAlPortapapeles()
    {
        if (RelayManager.instance == null || string.IsNullOrEmpty(RelayManager.instance.JoinCode))
            return;

        GUIUtility.systemCopyBuffer = RelayManager.instance.JoinCode;
        MostrarEstadoConexion("✓ Código copiado al portapapeles.");
    }

  
    public void ContinuarComoHost()
    {
        NetworkModeData.modoSeleccionado = NetworkModeData.Mode.Host;
        
        //El Host es quien controla la carga de escena con Netcode
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            EjecutarCargaDeNivelComoHost(primerNivelIndex);
        }
        else
        {
            Debug.LogError("[MainMenuManager] ContinuarComoHost() llamado pero NetworkManager no está listo o no somos Host.");
        }
    }

    // ─── Cliente (P2) — se une con el código ────────────────────────────────

    public void AbrirPantallaCliente()
    {
        if (panelBotonesMultiplayer != null) panelBotonesMultiplayer.SetActive(false);
        if (panelClientJoin         != null) panelClientJoin.SetActive(true);
        LimpiarEstadoConexion();
    }

    public async void ConfirmarCliente()
    {
        string codigo = inputIPCliente != null ? inputIPCliente.text.Trim() : "";

        if (string.IsNullOrEmpty(codigo))
        {
            MostrarEstadoConexion("⚠️ Escribe el código de sala.");
            return;
        }

        MostrarEstadoConexion("Conectando...");
        if (loadingSpinner != null) loadingSpinner.SetActive(true);

        bool conectado = await RelayManager.instance.JoinRelayAsClient(codigo);

        if (loadingSpinner != null) loadingSpinner.SetActive(false);

        if (!conectado)
        {
            MostrarEstadoConexion("❌ Código inválido o la sala ya no existe.");
            return;
        }

        NetworkModeData.modoSeleccionado = NetworkModeData.Mode.Cliente;
        
        //El Cliente NUNCA carga la escena manualmente.
        //Solo espera a que Netcode sincronice cuando el Host cargue.
        MostrarEstadoConexion("✓ Conectado. Esperando que el Host cargue el nivel...");
        
        // Cerrar paneles del menú y esperar la sincronización automática
        if (panelNetwork != null) panelNetwork.SetActive(false);
        if (panelClientJoin != null) panelClientJoin.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ESTADO DE CONEXIÓN (feedback al usuario)
    // ─────────────────────────────────────────────────────────────────────────

    private void MostrarEstadoConexion(string mensaje)
    {
        if (textoEstadoConexion != null)
            textoEstadoConexion.text = mensaje;

        Debug.Log($"[MainMenuManager] {mensaje}");
    }

    private void LimpiarEstadoConexion()
    {
        if (textoEstadoConexion != null) textoEstadoConexion.text = "";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OPCIONES
    // ─────────────────────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────────────────────
    // SCOREBOARD
    // ─────────────────────────────────────────────────────────────────────────

    public void MostrarScoreboard()
    {
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(false);
        if (panelScoreboard    != null) panelScoreboard.SetActive(true);

        foreach (Transform child in contentContenedor)
            Destroy(child.gameObject);

        if (RecordSystem.instance == null) return;

        foreach (var record in RecordSystem.instance.GetAll())
        {
            GameObject nuevaFila = Instantiate(filaPrefab, contentContenedor);
            TextMeshProUGUI[] textos = nuevaFila.GetComponentsInChildren<TextMeshProUGUI>();

            if (textos.Length >= 2)
            {
                textos[0].text = record.levelName;
                int m = Mathf.FloorToInt(record.bestTime / 60f);
                int s = Mathf.FloorToInt(record.bestTime % 60f);
                textos[1].text = $"{m:00}:{s:00}";
            }
        }
    }

    public void CerrarScoreboard()
    {
        if (panelScoreboard    != null) panelScoreboard.SetActive(false);
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SALIR
    // ─────────────────────────────────────────────────────────────────────────

    public void Salir()
    {
        Debug.Log("Saliendo del juego...");
        Application.Quit();

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HELPERS PRIVADOS
    // ─────────────────────────────────────────────────────────────────────────

    private void ActualizarBotones()
    {
        bool haySave = SaveSystem.instance != null && SaveSystem.instance.HasSaveFile();
        if (continuarButton != null) continuarButton.gameObject.SetActive(haySave);
    }


    private void EjecutarCargaDeNivel(int indexEscena)
    {
        if (LoadingScreenManager.Instance != null)
        {
            // LoadingScreenManager usa SceneManager.LoadSceneAsync internamente
            // Esto es correcto para solitario, ya que no hay Netcode
            LoadingScreenManager.Instance.LoadScene(indexEscena);
        }
        else
        {
            SceneManager.LoadScene(indexEscena);
        }
    }


    private void EjecutarCargaDeNivelComoHost(int sceneIndex)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[MainMenuManager] NetworkManager.Singleton es null. No se puede cargar la escena con Netcode.");
            return;
        }

        if (!NetworkManager.Singleton.IsHost)
        {
            Debug.LogError("[MainMenuManager] EjecutarCargaDeNivelComoHost() solo debe ser llamado por el Host.");
            return;
        }

        Debug.Log($"[MainMenuManager] Host cargando escena {sceneIndex} con NetworkManager.SceneManager...");

        NetworkManager.Singleton.SceneManager.LoadScene("Nivel1", LoadSceneMode.Single);
    }
}