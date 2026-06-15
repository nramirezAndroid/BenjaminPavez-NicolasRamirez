using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gestiona la conexión de red y el spawn de jugadores en modo cooperativo.
/// - Host = P1 (platformer)
/// - Cliente = P2 (dios/cooperativo)
/// No hay intercambio de roles ni sistema de rondas.
/// </summary>
public class AsymmetricNetworkManager : MonoBehaviour
{
    [Header("Prefabs de Jugadores")]
    public GameObject platformerPrefab;   // P1: el que corre y salta
    public GameObject godPrefab;          // P2: el que ayuda desde arriba

    [Header("Puntos de Aparición")]
    public Transform platformerSpawnPoint;

    [Header("Pantalla de Espera (Host)")]
    public GameObject panelEsperandoJugador;

    private bool esModoSolitario = false;

    // ─────────────────────────────────────────────────────────────────────────
    // Inicio
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback    += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback   += HandleClientDisconnect;

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        if (panelEsperandoJugador != null) panelEsperandoJugador.SetActive(false);

        switch (NetworkModeData.modoSeleccionado)
        {
            case NetworkModeData.Mode.Solitario:
                esModoSolitario = true;
                NetworkManager.Singleton.StartHost();
                break;

            case NetworkModeData.Mode.Host:
                esModoSolitario = false;
                transport.ConnectionData.Address = "0.0.0.0";
                NetworkManager.Singleton.StartHost();

                if (panelEsperandoJugador != null) panelEsperandoJugador.SetActive(true);
                Time.timeScale = 0f;  // Pausamos hasta que P2 se conecte
                break;

            case NetworkModeData.Mode.Cliente:
                esModoSolitario = false;
                transport.ConnectionData.Address = NetworkModeData.ipDelHost;
                NetworkManager.Singleton.StartClient();
                break;

            default:
                Debug.LogWarning("Modo no especificado. Iniciando en Solitario por defecto.");
                esModoSolitario = true;
                NetworkManager.Singleton.StartHost();
                break;
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
        GameObject prefabToSpawn;
        Vector3 spawnPosition = Vector3.zero;

        if (esModoSolitario)
        {
            // Modo solitario: solo P1 existe, siempre el platformer
            prefabToSpawn = platformerPrefab;
            if (platformerSpawnPoint != null) spawnPosition = platformerSpawnPoint.position;
        }
        else
        {
            bool isHost = (clientId == NetworkManager.ServerClientId);

            if (isHost)
            {
                // Host siempre es P1 (platformer) en modo cooperativo
                prefabToSpawn = platformerPrefab;
                if (platformerSpawnPoint != null) spawnPosition = platformerSpawnPoint.position;
            }
            else
            {
                // El cliente que se une siempre es P2 (god/cooperativo)
                prefabToSpawn = godPrefab;
                // P2 no necesita spawn point fijo; aparece donde sea
            }
        }

        GameObject playerInstance = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
        playerInstance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);

        // Notificamos al CoopManager quién se conectó
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
        SceneManager.LoadScene(0);
    }
}
