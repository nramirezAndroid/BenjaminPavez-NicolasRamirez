using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

// CoopManager es MonoBehaviour (no NetworkBehaviour) porque usa DontDestroyOnLoad en Awake,
// lo que lo saca de la escena antes de que NGO pueda spawnearlo. Como NetworkBehaviour no
// spawneado, sus [ClientRpc] y NetworkVariable nunca funcionan y NGO emite el warning
// "You may not pass in objects that are already persistent". Como MonoBehaviour, la
// comunicación con el CLIENTE se hace a través de PlayerController (que sí está spawneado).
public class CoopManager : MonoBehaviour
{
    public static CoopManager instance;

    [Header("Configuración")]
    [SerializeField] private int levelSceneIndex;

    [Header("UI cooperativa")]
    public GameObject p2HUD;              //panel con controles de P2 (buffos, trampas)

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // Transferir referencias de UI de la nueva escena al singleton existente.
            // El singleton persiste entre escenas (DontDestroyOnLoad), pero sus campos
            // [SerializeField] apuntan a objetos de la escena anterior (ya destruidos).
            // Al entrar a una nueva escena se instancia un nuevo CoopManager con las
            // referencias correctas; las copiamos aquí antes de destruirlo para que
            // MostrarVictoriaLocal/MostrarDerrotaLocal funcionen en la escena actual.
            if (p2HUD != null) instance.p2HUD = this.p2HUD;

            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (p2HUD != null)
            p2HUD.SetActive(true);
    }

    public void RegisterPlayer1() { }

    public void RegisterPlayer2() { }

    public void OnGoalReached(float completionTime, bool esVictoriaFinal, string nombreSiguienteNivel = null)
    {
        bool esServidor = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        Debug.Log($"[CoopManager] OnGoalReached — esServidor={esServidor}, " +
                  $"esVictoriaFinal={esVictoriaFinal}, nivel='{nombreSiguienteNivel}'");

        if (!esServidor) return;

        if (string.IsNullOrEmpty(nombreSiguienteNivel) && !esVictoriaFinal)
        {
            Debug.LogWarning("[CoopManager] OnGoalReached: nombreSiguienteNivel vacío, abortando.");
            return;
        }

        if (esVictoriaFinal)
        {
            //Mostrar en HOST directamente (CoopManager no está spawneado → su ClientRpc no llega)
            MostrarVictoriaLocal(completionTime);

            //Enviar al CLIENT a través del PlayerController que SÍ está spawneado
            PlayerController p1 = PlayerTargetFinder.GetPlayer1();
            if (p1 != null && p1.IsSpawned)
                p1.MostrarVictoriaClientRpc(completionTime);
            else
                Debug.LogWarning("[CoopManager] MostrarVictoriaClientRpc no enviado: P1 no disponible.");
        }
        else
        {
            CoopNetworkManager.EstaTransicionandoEscena = true;
            Time.timeScale = 1f;

            StartCoroutine(CargarEscenaHost(nombreSiguienteNivel));
        }
    }

    private System.Collections.IEnumerator CargarEscenaHost(string nombreSiguienteNivel)
    {
        //espera 1 frame para que los mensajes en vuelo salgan por la red.
        yield return null;

        PlayerController p1 = PlayerTargetFinder.GetPlayer1();

        // PASO 1: notificar al CLIENTE que estamos en transición ANTES de cargar la escena.
        // Esto pone EstaTransicionandoEscena=true en P2, evitando que HandleClientDisconnect
        // lo mande al menú cuando NGO despawnea los objetos del nivel actual.
        if (p1 != null && p1.IsSpawned)
            p1.NotificarTransicionClientRpc();
        else
            Debug.LogWarning("[CoopManager] NotificarTransicion: P1 no disponible.");

        yield return null; //1 frame para que NotificarTransicion salga por la red

        // PASO 2: ordenar al CLIENTE que cargue la siguiente escena de forma directa.
        // No usamos NGO SceneManager porque la sincronización NGO puede no estar disponible
        // (RequireAuthenticatedPackets, Relay disconnect, etc.) o llegar antes que
        // NotificarTransicion, provocando que HandleClientDisconnect expulse a P2.
        // CargarSiguienteNivelClientRpc también refuerza EstaTransicionandoEscena=true en P2.
        if (p1 != null && p1.IsSpawned)
            p1.CargarSiguienteNivelClientRpc(nombreSiguienteNivel);
        else
            Debug.LogWarning("[CoopManager] CargarSiguienteNivel: P1 no disponible para notificar al CLIENT.");

        yield return null; //1 frame para que el RPC salga por la red antes de que el HOST cambie de escena

        // PASO 3: el HOST carga la siguiente escena.
        // SpawnJugadoresParaTransicion() espera 1.5 s (tiempo real) antes de spawnear,
        // dando al CLIENTE tiempo suficiente para terminar de cargar la nueva escena
        // y recibir los spawn messages sin timeouts.
        Debug.Log($"[CoopManager] HOST: cargando '{nombreSiguienteNivel}'.");
        SceneManager.LoadScene(nombreSiguienteNivel);
    }

    //Llamado directamente en el HOST y vía PlayerController.MostrarVictoriaClientRpc en el CLIENT.
    //No usa ClientRpc porque CoopManager no está spawneado como NetworkObject.
    public void MostrarVictoriaLocal(float completionTime)
    {
        if (GameManager.instance != null)
            GameManager.instance.WinLevel();
        else
            Debug.LogWarning("[CoopManager] MostrarVictoriaLocal: GameManager no disponible.");
    }

    public void OnPlayer1Died()
    {
        bool esServidor = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        if (!esServidor) return;

        // Mostrar derrota en el HOST directamente
        MostrarDerrotaLocal();

        // Notificar al CLIENTE a través de PlayerController (que sí está spawneado)
        PlayerController p1 = PlayerTargetFinder.GetPlayer1();
        if (p1 != null && p1.IsSpawned)
            p1.MostrarDerrotaClientRpc();
        else
            Debug.LogWarning("[CoopManager] MostrarDerrotaClientRpc no enviado: P1 no disponible.");
    }

    // Llamado localmente en HOST y vía PlayerController.MostrarDerrotaClientRpc en CLIENTE.
    // P1 reaparece automáticamente tras 1.5s (DeathRespawnRoutine), así que no se bloquea el juego.
    // Si quieres mostrar una pantalla de derrota, conéctala aquí.
    public void MostrarDerrotaLocal() { }

    public void RetryLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene(0);
    }

}
