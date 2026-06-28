using UnityEngine;
using Unity.Netcode;

public static class PlayerTargetFinder
{
    private static PlayerController cachedPlayer1;
    private static float lastSearchTime = -1f;
    private const float SEARCH_INTERVAL = 2f; //buscar más frecuentemente (cada 2 segundos)

    public static PlayerController GetPlayer1()
    {
        //si ya lo tenemos y está vivo, devuelve el cacheado
        if (cachedPlayer1 != null && !cachedPlayer1.IsDead)
            return cachedPlayer1;

        //cuando P1 no está cacheado: buscar sin throttle (P1 puede no haber spawneado aún
        //en el cliente — los mensajes de red llegan varios frames después de OnNetworkSpawn).
        //cuando P1 sí está cacheado pero murió: throttle normal para no spamear.
        bool sinCache = (cachedPlayer1 == null);
        if (sinCache || Time.time - lastSearchTime > SEARCH_INTERVAL)
        {
            lastSearchTime = Time.time;
            RefreshCachedPlayer1();
        }

        return cachedPlayer1;
    }

    public static void ForceRefresh()
    {
        Debug.Log("[PlayerTargetFinder] Forzando búsqueda de Player 1...");
        lastSearchTime = Time.time - SEARCH_INTERVAL;
        RefreshCachedPlayer1();
    }

    private static void RefreshCachedPlayer1()
    {
        //estrategia 1: Verificar si el caché anterior sigue siendo válido
        if (cachedPlayer1 != null && !cachedPlayer1.IsDead)
        {
            return;
        }

        //estrategia 2: Buscar por tag "Player"
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            PlayerController player = playerObj.GetComponent<PlayerController>();
            if (player != null)
            {
                cachedPlayer1 = player;
                Debug.Log("[PlayerTargetFinder] ✓ Player 1 encontrado por TAG: " + player.gameObject.name);
                return;
            }
        }

        //estrategia 3: Buscar por tipo (incluye objetos inactivos — FindAnyObjectByType los omite)
        PlayerController[] allPlayers = Object.FindObjectsByType<PlayerController>(
            UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
        if (allPlayers.Length > 0)
        {
            cachedPlayer1 = allPlayers[0];
            Debug.Log("[PlayerTargetFinder] ✓ Player 1 encontrado por TIPO (incl. inactivos): " + cachedPlayer1.gameObject.name);
            return;
        }

        //estrategia 4: Buscar en NetworkManager (si está usando Netcode)
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.Count > 0)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
            {
                //playerObject puede ser null si el jugador aún no ha spawneado
                if (client.PlayerObject == null) continue;

                foreach (var playerNetworkObject in client.PlayerObject.GetComponentsInChildren<PlayerController>())
                {
                    if (playerNetworkObject != null)
                    {
                        cachedPlayer1 = playerNetworkObject;
                        Debug.Log("[PlayerTargetFinder] ✓ Player 1 encontrado por NETCODE: " + playerNetworkObject.gameObject.name);
                        return;
                    }
                }
            }
        }

        //no encontrado
        cachedPlayer1 = null;
        Debug.LogWarning("[PlayerTargetFinder] ⚠️ Player 1 NO ENCONTRADO. Estrategias agotadas.");
    }

    /// <summary>
    /// Registra directamente una instancia de P1 en el caché.
    /// Llamado desde PlayerController.OnNetworkSpawn() en el CLIENTE para evitar
    /// depender de FindGameObjectWithTag/FindAnyObjectByType, que fallan si el objeto
    /// está momentáneamente inactivo durante la inicialización de NGO.
    /// </summary>
    public static void RegisterPlayer1(PlayerController p1)
    {
        cachedPlayer1 = p1;
        lastSearchTime = Time.time;
        Debug.Log("[PlayerTargetFinder] ✓ P1 registrado directamente: " + p1.gameObject.name);
    }

    public static void Clear()
    {
        cachedPlayer1 = null;
        lastSearchTime = -1f;
        Debug.Log("[PlayerTargetFinder] Cache limpiado");
    }
}