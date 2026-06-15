using System.Net;
using System.Net.Sockets;
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
    
    [Header("Panel de Red (Multijugador)")]
    public GameObject panelNetwork; 

    [Header("Sub-Paneles de Red")]
    public GameObject panelBotonesMultiplayer;
    
    public GameObject panelHostWait;      //Pantalla para mostrar la IP al Host
    public TextMeshProUGUI textoIPHost;   //Texto donde mostraremos su IP
    
    public GameObject panelClientJoin;    //Pantalla para que el Cliente escriba
    public TMP_InputField inputIPCliente; //Campo donde el cliente escribe la IP

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

        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(true);
        if (panelConfirmacion  != null) panelConfirmacion.SetActive(false);
        if (panelOpciones      != null) panelOpciones.gameObject.SetActive(false);
        if (panelScoreboard    != null) panelScoreboard.SetActive(false);
        if (panelNetwork       != null) panelNetwork.SetActive(false); 
        
        //Asegurarnos de que los sub-paneles estén apagados al inicio
        if (panelBotonesMultiplayer != null) panelBotonesMultiplayer.SetActive(false);
        if (panelHostWait      != null) panelHostWait.SetActive(false);
        if (panelClientJoin    != null) panelClientJoin.SetActive(false);
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



    public void AbrirPanelMultijugador()
    {
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(false);
        if (panelNetwork != null) panelNetwork.SetActive(true);
        
        if (panelBotonesMultiplayer != null) panelBotonesMultiplayer.SetActive(true);
        if (panelHostWait != null) panelHostWait.SetActive(false);
        if (panelClientJoin != null) panelClientJoin.SetActive(false);
    }

    public void CerrarPanelMultijugador() 
    {
        if (panelNetwork != null) panelNetwork.SetActive(false);
        if (panelBotonesMultiplayer != null) panelBotonesMultiplayer.SetActive(false);
        if (panelHostWait != null) panelHostWait.SetActive(false);
        if (panelClientJoin != null) panelClientJoin.SetActive(false);
        
        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(true);
    }

    public void VolverSeleccionMultiplayer()
    {
        if (panelHostWait != null) panelHostWait.SetActive(false);
        if (panelClientJoin != null) panelClientJoin.SetActive(false);
        
        if (panelBotonesMultiplayer != null) panelBotonesMultiplayer.SetActive(true);
    }

    public void AbrirPantallaHost()
    {
        //Apagamos los botones iniciales ("Create Lobby", "Join Lobby", etc.)
        if (panelBotonesMultiplayer != null) panelBotonesMultiplayer.SetActive(false);
        
        if (panelHostWait != null) panelHostWait.SetActive(true);
        if (textoIPHost != null) textoIPHost.text = "Tu IP es: " + ObtenerIPLocal();
    }

    public void ConfirmarHost()
    {
        NetworkModeData.modoSeleccionado = NetworkModeData.Mode.Host;
        EjecutarCargaDeNivel(primerNivelIndex);
    }

    public void AbrirPantallaCliente()
    {
        //Apagamos los botones iniciales
        if (panelBotonesMultiplayer != null) panelBotonesMultiplayer.SetActive(false);
        
        if (panelClientJoin != null) panelClientJoin.SetActive(true);
    }

    public void ConfirmarCliente()
    {
        NetworkModeData.modoSeleccionado = NetworkModeData.Mode.Cliente;
        
        string ipIngresada = inputIPCliente.text.Trim();
        NetworkModeData.ipDelHost = string.IsNullOrEmpty(ipIngresada) ? "127.0.0.1" : ipIngresada;
        
        EjecutarCargaDeNivel(primerNivelIndex); 
    }

    private string ObtenerIPLocal()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return "127.0.0.1";
    }

    private void EjecutarCargaDeNivel(int indexEscena)
    {
        if (LoadingScreenManager.Instance != null)
        {
            LoadingScreenManager.Instance.LoadScene(indexEscena);
        }
        else
        {
            SceneManager.LoadScene(indexEscena);
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

        foreach (Transform child in contentContenedor)
        {
            Destroy(child.gameObject);
        }

        if (RecordSystem.instance == null) return;

        foreach (var record in RecordSystem.instance.GetAll())
        {
            GameObject nuevaFila = Instantiate(filaPrefab, contentContenedor);
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