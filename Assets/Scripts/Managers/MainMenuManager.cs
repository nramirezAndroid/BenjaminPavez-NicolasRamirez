using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class MainMenuManager : MonoBehaviour
{
    [Header("Paneles del Menú")]
    [SerializeField] private GameObject panelMenuPrincipal;
    [SerializeField] private GameObject panelConfirmacion;
    [SerializeField] private OptionsManager panelOpciones;
    [SerializeField] private GameObject panelScoreboard;

    [Header("Panel de Red (Cooperativo)")]
    [SerializeField] private GameObject panelNetwork;

    [Header("Sub-paneles de Red")]
    [SerializeField] private GameObject panelBotonesMultiplayer;

    [SerializeField] private GameObject panelHostWait;
    [SerializeField] private TextMeshProUGUI textoIPHost;
    [SerializeField] private Button botonContinuarHost;

    [SerializeField] private GameObject panelClientJoin;
    [SerializeField] private TMP_InputField inputIPCliente;
    [SerializeField] private Button botonUnirseCliente;

    [Header("Estado de Conexión")]
    [Tooltip("Texto opcional para mostrar 'Conectando...' / errores")]
    [SerializeField] private TextMeshProUGUI textoEstadoConexion;
    [SerializeField] private GameObject loadingSpinner;

    [Header("Scoreboard")]
    [SerializeField] private GameObject filaPrefab;
    [SerializeField] private Transform contentContenedor;

    [Header("Botones del Menú")]
    [SerializeField] private Button nuevaPartidaButton;
    [SerializeField] private Button continuarButton;
    [SerializeField] private Button cooperativoButton;
    [SerializeField] private Button opcionesButton;
    [SerializeField] private Button salirButton;

    [Header("Botones de Confirmación")]
    [SerializeField] private Button confirmSiButton;
    [SerializeField] private Button confirmNoButton;

    [Header("Escenas")]
    [SerializeField] private int primerNivelIndex;

    void Start()
    {
        Time.timeScale   = 1f;
        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;

        if (SaveSystem.instance == null)
            Debug.LogError("No hay SaveSystem en la escena.");

        //garantizamos que NetworkManager sobreviva el cambio de escena sin importar
        //la configuración del Inspector (DontDestroy checkbox del componente).
        if (NetworkManager.Singleton != null)
            DontDestroyOnLoad(NetworkManager.Singleton.gameObject);

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

        //limitar el campo de código: solo alfanumérico, máximo 6 caracteres
        if (inputIPCliente != null)
        {
            inputIPCliente.characterLimit = 6;
            inputIPCliente.contentType    = TMPro.TMP_InputField.ContentType.Alphanumeric;
        }

        //el NetworkManager tiene una propiedad "OfflineScene" que recarga la escena
        //automáticamente cuando el cliente se desconecta. Si queda configurada,
        //un join fallido (el SDK hace Shutdown internamente) destruye toda la UI.
        //la vaciamos por código para evitarlo.
        LimpiarOfflineSceneNetworkManager();

        //si el SDK recargó la escena mientras procesaba un join fallido,
        //relayManager (DontDestroyOnLoad) habrá guardado el mensaje de error.
        //lo recuperamos aquí y mostramos el panel de cliente con el error.
        string pendingError = RelayManager.instance != null ? RelayManager.instance.PendingConnectionError : null;
        Debug.Log($"[MainMenuManager] Start() — PendingConnectionError: '{pendingError ?? "(null)"}'");

        if (!string.IsNullOrEmpty(pendingError))
        {
            RelayManager.instance.PendingConnectionError = null;
            RestaurarPanelCliente();
            MostrarEstadoConexion(pendingError);
            Debug.Log($"[MainMenuManager] Mostrando error de conexión pendiente en panel cliente.");
        }
    }

    private void LimpiarOfflineSceneNetworkManager()
    {
        if (NetworkManager.Singleton == null) return;
        try
        {
            //el campo es serializado pero privado; usamos reflexión para vaciarlo.
            //probamos los nombres conocidos de distintas versiones de NGO.
            string[] candidatos = { "m_OfflineScene", "OfflineScene", "offlineScene" };
            foreach (string nombre in candidatos)
            {
                var campo = typeof(NetworkManager).GetField(nombre,
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public    |
                    System.Reflection.BindingFlags.Instance);
                if (campo != null)
                {
                    campo.SetValue(NetworkManager.Singleton, string.Empty);
                    Debug.Log($"[MainMenuManager] OfflineScene limpiado (campo '{nombre}').");
                    break;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MainMenuManager] No se pudo limpiar OfflineScene vía reflexión: {e.Message}");
        }
    }

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
        //si ya estábamos conectados esperando al host → desconectarse pero quedarse
        //en panelClientJoin para que el usuario pueda reintentar con otro código
        if (NetworkModeData.modoSeleccionado == NetworkModeData.Mode.Cliente)
        {
            NetworkModeData.modoSeleccionado = NetworkModeData.Mode.Ninguno;
            _ = RelayManager.instance?.LeaveSession();
            SetBotonUnirseInteractable(true);
            LimpiarEstadoConexion();
            return; //no navegar hacia atrás: el usuario reintenta desde el mismo panel
        }

        //navegación normal (no conectado): volver a selección Host/Cliente
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
            textoIPHost.text = $"Lobby Code: {joinCode}";

        MostrarEstadoConexion("Share the Lobby Code with your friend and Press Continue as Host.");

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
        //modo online siempre empieza sin datos de partida guardada
        if (SaveSystem.instance != null)
        {
            SaveSystem.instance.pendingLoad   = null;
            SaveSystem.instance.isLoadingGame = false;
        }

        NetworkModeData.modoSeleccionado = NetworkModeData.Mode.Host;

        //el Host es quien controla la carga de escena con Netcode
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            EjecutarCargaDeNivelComoHost(primerNivelIndex);
        }
        else
        {
            Debug.LogError("[MainMenuManager] ContinuarComoHost() llamado pero NetworkManager no está listo o no somos Host.");
        }
    }

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

        //validar formato antes de llamar al SDK (evita llamadas innecesarias)
        codigo = codigo.ToUpper();
        if (!System.Text.RegularExpressions.Regex.IsMatch(codigo, @"^[A-Z0-9]{1,6}$"))
        {
            MostrarEstadoConexion("⚠️ Código inválido. Solo letras y números, máximo 6 caracteres.");
            return;
        }

        //si ya estábamos conectados esperando al host, desconectarse primero
        if (NetworkModeData.modoSeleccionado == NetworkModeData.Mode.Cliente)
        {
            NetworkModeData.modoSeleccionado = NetworkModeData.Mode.Ninguno;
            if (RelayManager.instance != null) await RelayManager.instance.LeaveSession();
            if (this == null) return;
        }

        SetBotonUnirseInteractable(false);
        MostrarEstadoConexion("Conectando...");
        if (loadingSpinner != null) loadingSpinner.SetActive(true);

        bool conectado = false;
        try
        {
            conectado = await RelayManager.instance.JoinRelayAsClient(codigo);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MainMenuManager] Excepción al conectar: {e.Message}");
            conectado = false;
        }

        //detener spinner siempre, independientemente del resultado
        if (loadingSpinner != null) loadingSpinner.SetActive(false);

        //guardia: si el SDK recargó la escena mientras esperábamos, este MonoBehaviour
        //fue destruido. RelayManager.PendingConnectionError persiste y Start() lo mostrará.
        if (this == null) return;

        if (!conectado)
        {
            //limpiar el error pendiente: lo manejamos aquí directamente, sin esperar a Start()
            if (RelayManager.instance != null) RelayManager.instance.PendingConnectionError = null;

            RestaurarPanelCliente();
            MostrarEstadoConexion("❌ Código inválido o sala no encontrada.\nVerifica que el P1 ya haya creado la sala y vuelve a intentarlo.");
            return;
        }

        //éxito: marcar modo cliente, restaurar botón Connect (el usuario puede
        //reconectarse si quiere cambiar de código), y mostrar estado de espera.
        //Cuando el host inicie el nivel, NGO SceneManager cargará la escena automáticamente.
        NetworkModeData.modoSeleccionado = NetworkModeData.Mode.Cliente;
        MostrarEstadoConexion("✓ Conectado. Esperando que el Host inicie el nivel...");
        SetBotonUnirseInteractable(true);
    }

    //Restaura el panel de cliente al estado inicial: paneles visibles, botón activo.
    //Punto único de restauración — se usa tanto en errores de join como en Start() al volver de un crash.
    private void RestaurarPanelCliente()
    {
        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale   = 1f;

        if (panelMenuPrincipal      != null) panelMenuPrincipal.SetActive(false);
        if (panelBotonesMultiplayer != null) panelBotonesMultiplayer.SetActive(false);
        if (panelNetwork            != null) panelNetwork.SetActive(true);
        if (panelClientJoin         != null) panelClientJoin.SetActive(true);
        if (loadingSpinner          != null) loadingSpinner.SetActive(false);
        SetBotonUnirseInteractable(true);
    }

    //Muestra u oculta el botón de unirse usando SetActive (más fiable que solo interactable:
    //algunos Animators de botón dejan el botón visualmente oculto al desactivar interactable
    //y no lo recuperan al reactivarlo sin un ciclo de SetActive).
    private void SetBotonUnirseInteractable(bool activo)
    {
        if (botonUnirseCliente != null)
        {
            botonUnirseCliente.gameObject.SetActive(activo);
            botonUnirseCliente.interactable = activo;
            return;
        }
        //fallback si el inspector no tiene el campo asignado
        if (panelClientJoin == null) return;
        Button[] btns = panelClientJoin.GetComponentsInChildren<Button>(true);
        //buscar el botón de unirse por nombre
        foreach (var b in btns)
        {
            string n = b.name.ToLower();
            if (n.Contains("unir") || n.Contains("conectar") || n.Contains("join") || n.Contains("confirm"))
            {
                b.gameObject.SetActive(activo);
                b.interactable = activo;
                return;
            }
        }
        //si no hay match por nombre, usar el ÚLTIMO botón (normalmente el de acción principal)
        if (btns.Length > 0)
        {
            btns[btns.Length - 1].gameObject.SetActive(activo);
            btns[btns.Length - 1].interactable = activo;
        }
    }

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
        if (continuarButton != null) continuarButton.gameObject.SetActive(haySave);
    }

    private void EjecutarCargaDeNivel(int indexEscena)
    {
        if (LoadingScreenManager.Instance != null)
        {
            //loadingScreenManager usa SceneManager.LoadSceneAsync internamente
            //esto es correcto para solitario, ya que no hay Netcode
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