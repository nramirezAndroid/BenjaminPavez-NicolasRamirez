using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gestiona el SPAWN de jugadores en modo cooperativo una vez que la conexión
/// de red ya fue establecida (vía Relay, gestionado por RelayManager + MainMenuManager).
///
/// - Host = P1 (platformer)
/// - Cliente = P2 (dios/cooperativo)
/// No hay intercambio de roles ni sistema de rondas.
///
/// IMPORTANTE: este script YA NO inicia la conexión (StartHost/StartClient).
/// El Multiplayer Services SDK (RelayManager.CreateRelayHost / JoinRelayAsClient)
/// configura el UnityTransport y arranca el NetworkManager automáticamente
/// como parte de crear/unirse a una sesión, ANTES de cargar esta escena.
/// Si el modo es Solitario, este script sí inicia el host local (sin Relay).
/// </summary>
public class CoopNetworkManager : MonoBehaviour
{
    [Header("Prefabs de Jugadores")]
    public GameObject platformerPrefab;   // P1: el que corre y salta
    public GameObject godPrefab;          // P2: el que ayuda desde arriba

    [Header("Puntos de Aparición")]
    public Transform platformerSpawnPoint;

    [Header("Pantalla de Espera (Host)")]
    public GameObject panelEsperandoJugador;

    private bool esModoSolitario = false;
    private readonly System.Collections.Generic.HashSet<ulong> clientesYaSpawneados = new System.Collections.Generic.HashSet<ulong>();

    // ─────────────────────────────────────────────────────────────────────────
    // Inicio
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback  += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;

        if (panelEsperandoJugador != null) panelEsperandoJugador.SetActive(false);

        esModoSolitario = NetworkModeData.modoSeleccionado == NetworkModeData.Mode.Solitario;

        // El modo Solitario no necesita Relay: es host local sin conexión externa.
        // Host/Cliente con Relay YA fueron iniciados desde el menú antes de cargar esta escena.
        if (esModoSolitario)
        {
            NetworkManager.Singleton.StartHost();
            // Acabamos de arrancar el Host en este mismo frame: su propia conexión
            // (ServerClientId) ya está activa, así que la suscripción de arriba
            // llegó a tiempo y HandleClientConnected lo spawneará normalmente.
        }
        else if (NetworkModeData.modoSeleccionado == NetworkModeData.Mode.Host)
        {
            // Por seguridad: si por algún motivo llegamos aquí sin que el Host
            // ya esté corriendo (NetworkManager.Singleton.IsListening == false),
            // algo falló en el paso de Relay del menú.
            if (!NetworkManager.Singleton.IsListening)
            {
                Debug.LogError("[CoopNetworkManager] El Host no está escuchando. " +
                    "¿Se llamó a RelayManager.CreateRelayHost() antes de cargar esta escena?");
                return;
            }

            // IMPORTANTE: el Host arrancó en el MENÚ (antes de cargar esta escena),
            // por lo que su propio evento OnClientConnectedCallback(ServerClientId)
            // ya se disparó y se perdió — nos suscribimos demasiado tarde para
            // capturarlo. Por eso spawneamos al Host manualmente aquí.
            if (NetworkManager.Singleton.ConnectedClients.ContainsKey(NetworkManager.ServerClientId))
            {
                SpawnPlayerForClient(NetworkManager.ServerClientId);
            }

            if (panelEsperandoJugador != null) panelEsperandoJugador.SetActive(true);
            Time.timeScale = 0f; // Pausamos hasta que P2 se conecte
        }
        else if (NetworkModeData.modoSeleccionado == NetworkModeData.Mode.Cliente)
        {
            if (!NetworkManager.Singleton.IsListening)
            {
                Debug.LogError("[CoopNetworkManager] El Cliente no está conectado. " +
                    "¿Se llamó a RelayManager.JoinRelayAsClient() antes de cargar esta escena?");
            }
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback  -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Conexiones
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleClientConnected(ulong clientId)
    {
        // Reanudamos cuando P2 se conecta
        if (NetworkManager.Singleton.IsServer && clientId != NetworkManager.ServerClientId)
        {
            if (panelEsperandoJugador != null) panelEsperandoJugador.SetActive(false);
            Time.timeScale = 1f;
        }

        if (!NetworkManager.Singleton.IsServer) return;

        SpawnPlayerForClient(clientId);
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        // Guarda contra doble-spawn: por ejemplo, si tanto Start() como
        // HandleClientConnected intentan spawnear al mismo Host.
        if (clientesYaSpawneados.Contains(clientId)) return;
        clientesYaSpawneados.Add(clientId);

        GameObject prefabToSpawn;
        Vector3 spawnPosition = Vector3.zero;

        if (esModoSolitario)
        {
            prefabToSpawn = platformerPrefab;
            if (platformerSpawnPoint != null) spawnPosition = platformerSpawnPoint.position;
        }
        else
        {
            bool isHost = (clientId == NetworkManager.ServerClientId);

            if (isHost)
            {
                prefabToSpawn = platformerPrefab;
                if (platformerSpawnPoint != null) spawnPosition = platformerSpawnPoint.position;
            }
            else
            {
                prefabToSpawn = godPrefab;
            }
        }

        GameObject playerInstance = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
        playerInstance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);

        NotifyCoopManager(clientId);
    }

    private void NotifyCoopManager(ulong clientId)
    {
        if (CoopManager.instance == null) return;

        bool isHost = (clientId == NetworkManager.ServerClientId);
        if (isHost || esModoSolitario)
            CoopManager.instance.RegisterPlayer1();
        else
            CoopManager.instance.RegisterPlayer2();
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        // En cooperativo, si alguno se desconecta, volvemos al menú
        Boton_SalirYDesconectar();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Salida
    // ─────────────────────────────────────────────────────────────────────────

    public void Boton_SalirYDesconectar()
    {
        Time.timeScale = 1f;
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        if (RelayManager.instance != null) RelayManager.instance.ResetJoinCode();
        SceneManager.LoadScene(0);
    }
}