using System.Collections;
using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CoopNetworkManager : MonoBehaviour
{
    [Header("Prefabs de Jugadores")]
    [SerializeField] private GameObject platformerPrefab;
    [SerializeField] private GameObject godPrefab;

    [Header("Puntos de Aparición")]
    [SerializeField] private Transform platformerSpawnPoint;

    [Header("Pantalla de Espera (Host)")]
    [SerializeField] private GameObject panelEsperandoJugador;

    private bool esModoSolitario = false;
    private readonly System.Collections.Generic.HashSet<ulong> clientesYaSpawneados = new System.Collections.Generic.HashSet<ulong>();

    //evita que HandleClientDisconnect mande al menú durante una transición de escena cooperativa.
    //CoopManager lo activa antes de llamar LoadScene; se desactiva al entrar a la nueva escena.
    public static bool EstaTransicionandoEscena = false;

    //inicio

    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("[CoopNetworkManager] NetworkManager.Singleton es null en Start(). Abortando.");
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback  += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;

        //limpiamos el caché estático de PlayerTargetFinder para que no devuelva el P1
        //destruido de la escena anterior durante los primeros frames de la nueva escena.
        //Solo en el SERVIDOR: en el CLIENTE, el caché null ya se limpia solo porque Unity
        //detecta el objeto destruido como null. Llamar Clear() en el CLIENTE podría borrar
        //una referencia válida que P1 registró directamente en OnNetworkSpawn.
        if (NetworkManager.Singleton.IsServer)
            PlayerTargetFinder.Clear();

        if (panelEsperandoJugador != null) panelEsperandoJugador.SetActive(false);

        esModoSolitario = NetworkModeData.modoSeleccionado == NetworkModeData.Mode.Solitario;

        //el modo Solitario no necesita Relay: es host local sin conexión externa.
        //host/Cliente con Relay YA fueron iniciados desde el menú antes de cargar esta escena.
        if (esModoSolitario)
        {
            EstaTransicionandoEscena = false;
            //Shutdown() en NGO es asíncrono: necesitamos esperar a que IsListening
            //sea false antes de llamar StartHost(), de lo contrario NGO lanza
            //"Can't start while listening". Usamos una coroutine para eso.
            StartCoroutine(IniciarSolitario());
        }
        else if (NetworkModeData.modoSeleccionado == NetworkModeData.Mode.Host)
        {
            //por seguridad: si por algún motivo llegamos aquí sin que el Host
            //ya esté corriendo (NetworkManager.Singleton.IsListening == false),
            //algo falló en el paso de Relay del menú.
            if (!NetworkManager.Singleton.IsListening)
            {
                Debug.LogError("[CoopNetworkManager] El Host no está escuchando. " +
                    "¿Se llamó a RelayManager.CreateRelayHost() antes de cargar esta escena?");
                return;
            }

            //Verificamos si P2 ya está conectado (transición de nivel o retry)
            //o si es la primera carga (P2 todavía no se ha unido vía Relay).
            bool p2YaConectado = false;
            foreach (var kv in NetworkManager.Singleton.ConnectedClients)
                if (kv.Key != NetworkManager.ServerClientId)
                    p2YaConectado = true;

            // Durante transiciones entre escenas (Level1→Level2, etc.), P2 ya estaba
            // conectado en el nivel anterior. NGO tarda unos frames en re-registrar a P2
            // en ConnectedClients después del cambio de escena, por lo que p2YaConectado
            // puede ser false momentáneamente aunque P2 siga conectado.
            // Congelar el juego en ese caso deja al HOST sin responder los heartbeats de
            // NGO → NGO desconecta a P2 por timeout. La solución: durante transiciones,
            // nunca mostrar el panel de espera ni congelar timeScale.
            if (!p2YaConectado && !EstaTransicionandoEscena)
            {
                //primera carga desde el menú: mostrar panel y congelar tiempo mientras P2 se une.
                if (panelEsperandoJugador != null) panelEsperandoJugador.SetActive(true);
                Time.timeScale = 0f;
            }

            //SIEMPRE usamos la coroutine con delay, sin importar si es transición o primera carga.
            //Razón: en transiciones, el CLIENT necesita tiempo para inicializar el contexto de NGO
            //en la nueva escena. Si spawneamos en Start() los mensajes llegan demasiado pronto
            //→ deferred timeout → objetos invisibles para P2. El delay de 1.5s garantiza que
            //el CLIENT esté listo para recibir spawn messages.
            //En primera carga, el delay tampoco importa porque el tiempo está congelado (Time.timeScale = 0).
            Debug.Log("[CoopNetworkManager] HOST: esperando 1.5s antes de spawnear jugadores.");
            StartCoroutine(SpawnJugadoresParaTransicion());
        }
        else if (NetworkModeData.modoSeleccionado == NetworkModeData.Mode.Cliente)
        {
            if (!NetworkManager.Singleton.IsListening)
            {
                Debug.LogError("[CoopNetworkManager] El Cliente no está conectado. " +
                    "¿Se llamó a RelayManager.JoinRelayAsClient() antes de cargar esta escena?");
            }

            // Resetear EstaTransicionandoEscena en el CLIENTE después de que la escena cargó.
            // CargarSiguienteNivelClientRpc pone el flag en true antes de cargar; si no se
            // resetea, HandleClientDisconnect ignora TODOS los eventos futuros → el CLIENT
            // nunca detecta una desconexión legítima del servidor.
            StartCoroutine(ResetTransicionClienteAfterDelay());
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

    private void Update()
    {
        //fallback: si el panel de espera sigue activo pero P2 ya está en ConnectedClients,
        //cerrarlo. Cubre race conditions donde el callback llegó antes de la suscripción
        //o durante la carga de escena y el panel quedó visible indefinidamente.
        if (panelEsperandoJugador == null || !panelEsperandoJugador.activeSelf) return;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        foreach (var kv in NetworkManager.Singleton.ConnectedClients)
        {
            if (kv.Key != NetworkManager.ServerClientId)
            {
                panelEsperandoJugador.SetActive(false);
                Time.timeScale = 1f;
                Debug.Log("[CoopNetworkManager] Fallback: P2 detectado en ConnectedClients → panel cerrado.");
                break;
            }
        }
    }

    //coroutine para transiciones de nivel: espera antes de spawnear jugadores
    //dando tiempo al CLIENT para que su contexto NGO esté listo en la nueva escena.
    private IEnumerator SpawnJugadoresParaTransicion()
    {
        //capturar ANTES del await si era una transición de escena o la primera carga.
        //EstaTransicionandoEscena puede cambiar durante el await (el Fallback Update lo usa).
        bool eraTransicion = EstaTransicionandoEscena;

        //WaitForSecondsRealtime ignora timeScale (que podría ser 0 durante la transición).
        yield return new WaitForSecondsRealtime(1.5f);

        EstaTransicionandoEscena = false;
        Debug.Log($"[CoopNetworkManager] HOST: spawneando jugadores (eraTransicion={eraTransicion}).");

        //limpiar el hash SOLO en transiciones entre niveles (Nivel1→Nivel2), donde los
        //objetos de red del nivel anterior ya fueron destruidos y necesitamos re-spawnear.
        //En la primera carga (MainMenu→Nivel1) NO limpiamos: si HandleClientConnected ya
        //spawneó a P2 mientras esperábamos, limpiar el hash causaría un doble-spawn.
        if (eraTransicion)
            clientesYaSpawneados.Clear();

        if (NetworkManager.Singleton.ConnectedClients.ContainsKey(NetworkManager.ServerClientId))
            SpawnPlayerForClient(NetworkManager.ServerClientId);

        foreach (var kv in NetworkManager.Singleton.ConnectedClients)
        {
            if (kv.Key != NetworkManager.ServerClientId)
                SpawnPlayerForClient(kv.Key);
        }
    }

    //coroutine cliente — resetea EstaTransicionandoEscena después de que la nueva escena cargó
    private IEnumerator ResetTransicionClienteAfterDelay()
    {
        //esperamos el mismo tiempo que el HOST tarda en empezar a spawnear (1.5 s),
        //más un margen extra. Así el flag cubre todo el período de carga+spawn.
        yield return new WaitForSecondsRealtime(2.5f);
        EstaTransicionandoEscena = false;
        Debug.Log("[CoopNetworkManager] CLIENTE: EstaTransicionandoEscena reseteado tras transición.");
    }

    //coroutine de inicio solitario — espera a que Shutdown termine antes de StartHost
    private IEnumerator IniciarSolitario()
    {
        //esperamos un frame para que todos los Start() de la escena se ejecuten
        //(ej: EnemySpawner.Start() necesita suscribirse a OnServerStarted antes
        //de que llamemos StartHost(), o el evento se dispara antes de la suscripción).
        yield return null;

        if (NetworkManager.Singleton == null) yield break;

        if (NetworkManager.Singleton.IsListening)
        {
            //desuscribimos temporalmente para que el evento de desconexión del Shutdown
            //no dispare Boton_SalirYDesconectar() y nos mande al menú.
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
            NetworkManager.Singleton.Shutdown();

            //esperamos a que NGO marque IsListening = false
            float timeout = 3f;
            while (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;

        }

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[CoopNetworkManager] NetworkManager desapareció durante el Shutdown.");
            yield break;
        }

        //en modo solitario usamos un puerto libre elegido por el SO para evitar
        //conflictos con sesiones anteriores que todavía tengan el puerto en TIME_WAIT.
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            ushort puertoLibre = ObtenerPuertoUdpLibre();
            transport.SetConnectionData("127.0.0.1", puertoLibre);
            Debug.Log($"[CoopNetworkManager] Puerto solitario asignado: {puertoLibre}");
        }

        NetworkManager.Singleton.StartHost();
        //el evento OnClientConnectedCallback(ServerClientId) se disparará sincrónicamente
        //dentro de StartHost(), así que HandleClientConnected spawneará al jugador.
    }

    //devuelve un puerto UDP disponible preguntándole directamente al SO
    private ushort ObtenerPuertoUdpLibre()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return (ushort)((IPEndPoint)socket.LocalEndPoint).Port;
    }

    //conexiones

    private void HandleClientConnected(ulong clientId)
    {
        //reanudamos cuando P2 se conecta
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
        //guarda contra doble-spawn: por ejemplo, si tanto Start() como
        //handleClientConnected intentan spawnear al mismo Host.
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
                //P2 (hada) spawnea junto a P1. Primero intentamos usar la posición
                //real de P1 (ya spawneado arriba). Si no está disponible, usamos
                //platformerSpawnPoint como fallback.
                ulong hostId = NetworkManager.ServerClientId;
                if (NetworkManager.Singleton.ConnectedClients.TryGetValue(hostId, out var hostClient)
                    && hostClient.PlayerObject != null)
                {
                    spawnPosition = hostClient.PlayerObject.transform.position + new Vector3(1f, 0f, 0f);
                    Debug.Log($"[CoopNetworkManager] P2 spawneando junto a P1 en {spawnPosition}");
                }
                else if (platformerSpawnPoint != null)
                {
                    spawnPosition = platformerSpawnPoint.position + new Vector3(1f, 0f, 0f);
                    Debug.Log($"[CoopNetworkManager] P2 spawneando en platformerSpawnPoint en {spawnPosition}");
                }
                else
                {
                    Debug.LogWarning("[CoopNetworkManager] P2: ni P1 ni platformerSpawnPoint disponibles, spawneando en (0,0,0)");
                }
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
        //si ya estamos en el menú (escena 0), ignorar: el SDK dispara este evento
        //al limpiar una conexión fallida desde el propio menú, y llamar LoadScene(0)
        //aquí recargaría la escena destruyendo la UI mientras ConfirmarCliente aún corre.
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex == 0)
            return;

        //durante una transición de escena cooperativa, los NetworkObjects del nivel anterior
        //se destruyen (destroyWithScene=true), lo que puede disparar desconexiones falsas.
        //ignoramos el evento para no interrumpir la carga del siguiente nivel.
        if (EstaTransicionandoEscena) return;

        //en cooperativo, si alguno se desconecta desde el juego, volvemos al menú
        Boton_SalirYDesconectar();
    }

    //salida

    public void Boton_SalirYDesconectar()
    {
        Time.timeScale = 1f;
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        if (RelayManager.instance != null) RelayManager.instance.ResetJoinCode();
        SceneManager.LoadScene(0);
    }
}