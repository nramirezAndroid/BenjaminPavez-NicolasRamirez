using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

/// <summary>
/// Maneja la conexión ONLINE real (sin LAN, sin port forwarding) usando el
/// Multiplayer Services SDK de Unity (com.unity.services.multiplayer),
/// que reemplaza al paquete standalone "Relay" (deprecado).
///
/// Bajo el capó sigue siendo Relay, pero ahora se gestiona a través del
/// concepto de "Session": el Host crea una sesión y recibe un código de
/// sala; el Cliente se une con ese código. El SDK configura el
/// UnityTransport automáticamente — ya no hace falta tocarlo a mano.
///
/// Reemplaza la conexión directa por IP de CoopNetworkManager.
/// Debe vivir en un GameObject persistente (DontDestroyOnLoad) o en la escena de menú.
/// </summary>
public class RelayManager : MonoBehaviour
{
    public static RelayManager instance;

    [Header("Configuración")]
    [Tooltip("Máximo de jugadores en la sesión, incluyendo al host. En modo coop 1vs1, esto es 2.")]
    public int maxPlayers = 2;

    // Sesión activa (válida tanto para host como para cliente una vez conectado)
    private ISession activeSession;

    // Código de sala generado al crear el host, usado para mostrarlo en UI
    public string JoinCode => activeSession?.Code;

    // Estado de inicialización de UGS
    private bool servicesInitialized = false;

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

    // ─────────────────────────────────────────────────────────────────────────
    // INICIALIZACIÓN DE UNITY GAMING SERVICES
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inicializa UGS y hace login anónimo. Debe llamarse antes de crear o unirse a una sala.
    /// Es seguro llamarlo varias veces; solo se inicializa una vez.
    /// </summary>
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

    // ─────────────────────────────────────────────────────────────────────────
    // HOST: Crear sesión y obtener código
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Crea una sesión usando Relay como transporte y configura el NetworkManager
    /// automáticamente como Host. Devuelve el código de sala (6 caracteres)
    /// para mostrarlo al jugador, o null si falló.
    /// </summary>
    public async Task<string> CreateRelayHost()
    {
        bool ready = await EnsureServicesInitialized();
        if (!ready) return null;

        try
        {
            var options = new SessionOptions
            {
                MaxPlayers = maxPlayers
            }.WithRelayNetwork(); // Usa Relay (NAT traversal) en vez de IP directa

            activeSession = await MultiplayerService.Instance.CreateSessionAsync(options);

            // El SDK ya configuró el UnityTransport y arrancó el NetworkManager como Host.
            // No hace falta llamar a NetworkManager.Singleton.StartHost() manualmente.

            Debug.Log($"[RelayManager] Sesión creada. Código: {activeSession.Code}");
            return activeSession.Code;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RelayManager] Error creando sesión: {e.Message}");
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CLIENTE: Unirse con código
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Se une a una sesión existente usando el código de 6 caracteres.
    /// Devuelve true si la conexión se inició correctamente.
    /// </summary>
    public async Task<bool> JoinRelayAsClient(string joinCode)
    {
        bool ready = await EnsureServicesInitialized();
        if (!ready) return false;

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            Debug.LogWarning("[RelayManager] Código de sala vacío.");
            return false;
        }

        try
        {
            activeSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode.Trim().ToUpper());

            // El SDK configura el UnityTransport y arranca el NetworkManager como Cliente automáticamente.

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RelayManager] Error al unirse a la sesión: {e.Message}");
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LIMPIEZA
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sale de la sesión activa (host o cliente) y limpia el estado local.
    /// Llamar al volver al menú principal o al desconectarse.
    /// </summary>
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
