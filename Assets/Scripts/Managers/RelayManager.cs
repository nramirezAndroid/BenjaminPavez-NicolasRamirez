using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

public class RelayManager : MonoBehaviour
{
    public static RelayManager instance;

    [Header("Configuración")]
    [Tooltip("Máximo de jugadores en la sesión, incluyendo al host. En modo coop 1vs1, esto es 2.")]
    [SerializeField] private int maxPlayers;

    //sesión activa (válida tanto para host como para cliente una vez conectado)
    private ISession activeSession;

    //código de sala generado al crear el host, usado para mostrarlo en UI
    public string JoinCode => activeSession?.Code;

    //estado de inicialización de UGS
    private bool servicesInitialized = false;

    public string PendingConnectionError { get; set; }

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

    public async Task<bool> EnsureServicesInitialized()
    {
        if (servicesInitialized) return true;

        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            servicesInitialized = true;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RelayManager] Error inicializando servicios: {e.Message}");
            return false;
        }
    }

    public async Task<string> CreateRelayHost()
    {
        bool ready = await EnsureServicesInitialized();
        if (!ready) return null;

        try
        {
            var options = new SessionOptions
            {
                MaxPlayers = maxPlayers
            }.WithRelayNetwork(); //usa Relay (NAT traversal) en vez de IP directa

            activeSession = await MultiplayerService.Instance.CreateSessionAsync(options);

            //el SDK ya configuró el UnityTransport y arrancó el NetworkManager como Host.
            //no hace falta llamar a NetworkManager.Singleton.StartHost() manualmente.

            Debug.Log($"[RelayManager] Sesión creada. Código: {activeSession.Code}");
            return activeSession.Code;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RelayManager] Error creando sesión: {e.Message}");
            return null;
        }
    }

    public async Task<bool> JoinRelayAsClient(string joinCode)
    {
        bool ready = await EnsureServicesInitialized();
        if (!ready) return false;

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            Debug.LogWarning("[RelayManager] Código de sala vacío.");
            return false;
        }

        //seteamos el error ANTES del primer await para evitar race conditions:
        //si el SDK recarga la escena durante el join, el error ya está guardado
        //y Start() lo mostrará aunque la continuación del catch llegue tarde.
        PendingConnectionError = "❌ Código inválido o sala no encontrada.\nVerifica que el P1 ya haya creado la sala y vuelve a intentarlo.";

        try
        {
            //await directo — sin Task.WhenAny + Task.Delay.
            //task.WhenAny deja un Task.Delay corriendo en background que, al disparar,
            //puede interferir con el SynchronizationContext de Unity y causar freeze.
            //el SDK de Lobby tiene su propio timeout HTTP interno; si el código es
            //inválido, la excepción llega en ~1 s (como vemos en los logs).
            activeSession = await MultiplayerService.Instance
                .JoinSessionByCodeAsync(joinCode.Trim().ToUpper());

            //éxito: limpiar el error preemptivo
            PendingConnectionError = null;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RelayManager] Join fallido: {e.Message}");
            //pendingConnectionError ya fue seteado antes del await.
            activeSession = null;
            return false;
        }
    }

    public async Task LeaveSession()
    {
        if (activeSession != null)
        {
            try
            {
                await activeSession.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RelayManager] Error al salir de la sesión: {e.Message}");
            }
            finally
            {
                activeSession = null;
            }
        }
    }

    public void ResetJoinCode()
    {
        activeSession = null;
    }
}
