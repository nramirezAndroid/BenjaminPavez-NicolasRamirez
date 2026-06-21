using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;


public class CoopManager : NetworkBehaviour
{
    public static CoopManager instance;

    [Header("Configuración")]
    public int levelSceneIndex = 1;

    [Header("UI cooperativa")]
    public TextMeshProUGUI modeText;       //Muestra "Modo Cooperativo"
    public TextMeshProUGUI statusText;     //Estado de conexión / progreso
    public GameObject resultsPanel;
    public TextMeshProUGUI resultsText;
    public GameObject p2HUD;              //Panel con controles de P2 (buffos, trampas)



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

    public override void OnNetworkSpawn()
    {
        p1Connected.OnValueChanged += (_, __) => RefreshStatusUI();
        p2Connected.OnValueChanged += (_, __) => RefreshStatusUI();

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
        if (!IsServer) return;

        if (esVictoriaFinal)
        {
            ShowVictoryClientRpc(completionTime);
        }
        else
        {
            AvanzarSiguienteNivelClientRpc(nombreSiguienteNivel);
        }
    }

    [ClientRpc]
    private void AvanzarSiguienteNivelClientRpc(string nombreSiguienteNivel)
    {
        //Nivel intermedio: no se muestra ningún panel, se sigue jugando sin pausa.
        Time.timeScale = 1f;

        if (string.IsNullOrEmpty(nombreSiguienteNivel))
        {
            Debug.LogWarning("[CoopManager] nombreSiguienteNivel vacío; no se puede avanzar de nivel.");
            return;
        }

        if (LoadingScreenManager.Instance != null)
            LoadingScreenManager.Instance.LoadScene(nombreSiguienteNivel);
        else
            SceneManager.LoadScene(nombreSiguienteNivel);
    }

    [ClientRpc]
    private void ShowVictoryClientRpc(float completionTime)
    {
        if (resultsPanel != null) resultsPanel.SetActive(true);

        int minutes = Mathf.FloorToInt(completionTime / 60f);
        int seconds = Mathf.FloorToInt(completionTime % 60f);
        string timeStr = $"{minutes:00}:{seconds:00}";

        if (resultsText != null)
            resultsText.text = $"¡Meta alcanzada!\nTiempo: {timeStr}\n¡Bien hecho, equipo!";
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
