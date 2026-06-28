using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;

public class CoopManager : NetworkBehaviour
{
    public static CoopManager instance;

    [Header("Configuración")]
    [SerializeField] private int levelSceneIndex;

    [Header("UI cooperativa")]
    [SerializeField] private TextMeshProUGUI modeText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private TextMeshProUGUI resultsText;
    public GameObject p2HUD;              //panel con controles de P2 (buffos, trampas)

    public NetworkVariable<bool> p1Connected = new NetworkVariable<bool>(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> p2Connected = new NetworkVariable<bool>(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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

    private bool yaSubscrito = false;

    public override void OnNetworkSpawn()
    {
        //evitar suscripciones duplicadas si OnNetworkSpawn se llama más de una vez
        if (!yaSubscrito)
        {
            p1Connected.OnValueChanged += (_, __) => RefreshStatusUI();
            p2Connected.OnValueChanged += (_, __) => RefreshStatusUI();
            yaSubscrito = true;
        }

        if (modeText != null)
            modeText.text = "Modo Cooperativo";

        if (p2HUD != null)
            p2HUD.SetActive(true);

        RefreshStatusUI();
    }

    public void RegisterPlayer1()
    {
        if (!IsServer) return;
        p1Connected.Value = true;
    }

    public void RegisterPlayer2()
    {
        if (!IsServer) return;
        p2Connected.Value = true;
    }

    public void OnGoalReached(float completionTime, bool esVictoriaFinal, string nombreSiguienteNivel = null)
    {
        //IMPORTANTE: NetworkBehaviour.IsServer devuelve false cuando el NetworkObject no está
        //spawneado (CoopManager llama DontDestroyOnLoad en Awake, por lo que NGO nunca lo
        //auto-spawnea). Usamos NetworkManager.Singleton.IsServer directamente.
        bool esServidor = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        Debug.Log($"[CoopManager] OnGoalReached — esServidor={esServidor}, IsSpawned={IsSpawned}, " +
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

            //Usamos NGO SceneManager para la transición: esto sincroniza el cambio de escena
            //en TODAS las máquinas (HOST + clientes) y espera a que todos carguen antes de
            //spawnar objetos. Así se evitan los deferred spawn timeouts en P2.
            StartCoroutine(CargarEscenaHost(nombreSiguienteNivel));
        }
    }

    private System.Collections.IEnumerator CargarEscenaHost(string nombreSiguienteNivel)
    {
        //espera 1 frame para que los mensajes en vuelo salgan por la red.
        yield return null;

        //Intentar con NGO SceneManager: sincroniza el contexto de escena en el CLIENT ANTES de
        //enviar spawn messages, evitando "Deferred OnSpawn: NetworkObject not found" para P1.
        bool usoNGO = false;
        try
        {
            var status = NetworkManager.Singleton.SceneManager.LoadScene(
                nombreSiguienteNivel,
                UnityEngine.SceneManagement.LoadSceneMode.Single);
            usoNGO = (status == Unity.Netcode.SceneEventProgressStatus.Started);
            if (!usoNGO)
                Debug.LogWarning($"[CoopManager] NGO SceneManager status={status} → usando fallback.");
            else
                Debug.Log($"[CoopManager] HOST: NGO SceneManager cargando '{nombreSiguienteNivel}'.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[CoopManager] NGO SceneManager excepción: {e.Message} → usando fallback.");
        }

        if (usoNGO) yield break; //NGO gestiona la carga en CLIENT también → terminamos aquí

        //--- FALLBACK: raw SceneManager ---
        //Notificar al CLIENT vía ClientRpc para que cargue la escena y active la bandera
        //EstaTransicionandoEscena antes de que los mensajes de spawn lleguen.
        PlayerController p1 = PlayerTargetFinder.GetPlayer1();
        if (p1 != null && p1.IsSpawned)
            p1.CargarSiguienteNivelClientRpc(nombreSiguienteNivel);
        else
            Debug.LogWarning("[CoopManager] Fallback: P1 no disponible para notificar al CLIENT.");

        yield return null; //1 frame extra para que el ClientRpc salga por la red
        Debug.Log($"[CoopManager] HOST: fallback raw SceneManager cargando '{nombreSiguienteNivel}'.");
        SceneManager.LoadScene(nombreSiguienteNivel);
    }

    //Llamado directamente en el HOST y vía PlayerController.MostrarVictoriaClientRpc en el CLIENT.
    //No usa ClientRpc porque CoopManager no está spawneado como NetworkObject.
    public void MostrarVictoriaLocal(float completionTime)
    {
        if (resultsPanel != null) resultsPanel.SetActive(true);

        int minutes = Mathf.FloorToInt(completionTime / 60f);
        int seconds = Mathf.FloorToInt(completionTime % 60f);
        string timeStr = $"{minutes:00}:{seconds:00}";

        if (resultsText != null)
            resultsText.text = $"¡Gracias por jugar!\nTiempo: {timeStr}\n¡Bien hecho, equipo!";
    }

    [ClientRpc]
    private void ShowVictoryClientRpc(float completionTime)
    {
        //Mantenido por compatibilidad; en cooperativo se usa MostrarVictoriaLocal vía PlayerController.
        MostrarVictoriaLocal(completionTime);
    }

    public void OnPlayer1Died()
    {
        if (!IsServer) return;
        ShowDefeatClientRpc();
    }

    [ClientRpc]
    private void ShowDefeatClientRpc()
    {
        if (resultsPanel != null) resultsPanel.SetActive(true);
        if (resultsText  != null)
            resultsText.text = "¡El equipo fue derrotado!\nInténtenlo de nuevo.";
    }

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

    private void RefreshStatusUI()
    {
        if (statusText == null) return;

        bool allConnected = p1Connected.Value && p2Connected.Value;
        statusText.text = allConnected
            ? "✓ Ambos jugadores conectados"
            : "Esperando jugadores...";
    }
}
