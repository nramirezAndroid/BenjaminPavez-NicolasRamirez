using UnityEngine;
using Unity.Netcode;

public class EnemySpawner : MonoBehaviour
{
    [Header("Prefabs de Enemigos")]
    [SerializeField] private GameObject slimesPrefab;
    [SerializeField] private GameObject enemyflyPrefab;
    [SerializeField] private GameObject enemyLongSwordKnightPrefab;

    [Header("Configuración de Spawn")]
    [Tooltip("Posiciones donde spawnear enemigos (configura en Inspector)")]
    public Transform[] spawnPoints;

    [Header("Debug")]
    [SerializeField] private bool debugMode;

    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("[EnemySpawner] NetworkManager.Singleton es null. Abortando spawn.");
            enabled = false;
            return;
        }

        // En modo solitario, CoopNetworkManager.IniciarSolitario() hace Shutdown()+StartHost()
        // UN FRAME después de Start(). Si spawneamos ahora, Shutdown() matará a todos los enemigos.
        // Por eso: en modo solitario SIEMPRE esperamos OnServerStarted (que se dispara tras StartHost).
        // En modo relay (Host/Cliente), el host ya está corriendo estable → spawneamos directo.
        bool esModoSolitario = NetworkModeData.modoSeleccionado == NetworkModeData.Mode.Solitario;

        if (!esModoSolitario && (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost))
        {
            //En relay, SIEMPRE esperamos 2.5s antes de spawnear enemigos.
            //Los jugadores se spawnean a 1.5s (ver CoopNetworkManager.SpawnJugadoresParaTransicion).
            //Esperando 2.5s garantizamos que: (1) el CLIENT esté listo para recibir spawn messages,
            //(2) P1 y P2 ya existan para que PlayerTargetFinder los encuentre desde el frame 1.
            //En primera carga, el delay tampoco importa porque el tiempo está congelado.
            Debug.Log("[EnemySpawner] Relay HOST: esperando 2.5s antes de spawnear enemigos.");
            StartCoroutine(SpawnEnemiesDelayed(2.5f));
        }
        else
        {
            //modo solitario o cliente: esperamos OnServerStarted para spawnear
            NetworkManager.Singleton.OnServerStarted += HandleServerStarted;
        }
    }

    private System.Collections.IEnumerator SpawnEnemiesDelayed(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        SpawnAllEnemies();
    }

    private void HandleServerStarted()
    {
        NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
        SpawnAllEnemies();
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
    }

    void SpawnAllEnemies()
    {
        //si no hay spawn points en el Inspector, usa los hijos del GameObject automáticamente
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            int childCount = transform.childCount;
            if (childCount == 0)
            {
                Debug.LogWarning("[EnemySpawner] No hay spawn points ni hijos configurados.");
                return;
            }
            spawnPoints = new Transform[childCount];
            for (int i = 0; i < childCount; i++)
                spawnPoints[i] = transform.GetChild(i);
        }

        int spawneados = 0;
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            Transform spawnPoint = spawnPoints[i];
            if (spawnPoint == null)
            {
                Debug.LogWarning($"[EnemySpawner] spawnPoints[{i}] es NULL — reasígnalo en el Inspector del EnemySpawner en Nivel2.");
                continue;
            }

            //determinamos qué tipo de enemigo spawnear basado en un patrón o tag
            GameObject enemyPrefab = SelectEnemyPrefab(spawnPoint);

            if (enemyPrefab == null)
            {
                Debug.LogWarning($"[EnemySpawner] No se pudo seleccionar prefab para {spawnPoint.name}");
                continue;
            }

            SpawnEnemyAt(enemyPrefab, spawnPoint.position, spawnPoint.rotation);
            spawneados++;
        }

        Debug.Log($"[EnemySpawner] ✓ Spawneados {spawneados}/{spawnPoints.Length} enemigos");
    }

    GameObject SelectEnemyPrefab(Transform spawnPoint)
    {
        //estrategia 1: Usar SpawnPointConfig si existe
        SpawnPointConfig config = spawnPoint.GetComponent<SpawnPointConfig>();
        if (config != null)
        {
            if (config.GetEnemyType() == SpawnPointConfig.EnemyType.Slimes)
                return slimesPrefab;
            else if (config.GetEnemyType() == SpawnPointConfig.EnemyType.Enemyfly)
                return enemyflyPrefab;
            else if (config.GetEnemyType() == SpawnPointConfig.EnemyType.Knight)
                return enemyLongSwordKnightPrefab;
        }

        //estrategia 2: Basarse en tags
        if (spawnPoint.CompareTag("SpawnSlimes"))
            return slimesPrefab;
        
        if (spawnPoint.CompareTag("SpawnEnemyfly"))
            return enemyflyPrefab;
        
        if (spawnPoint.CompareTag("SpawnKnight"))
            return enemyLongSwordKnightPrefab;

        //estrategia 3: Ciclar entre enemigos por defecto
        int index = System.Array.IndexOf(spawnPoints, spawnPoint);
        
        if (index % 3 == 0)
            return slimesPrefab;
        else if (index % 3 == 1)
            return enemyflyPrefab;
        else
            return enemyLongSwordKnightPrefab;
    }

    void SpawnEnemyAt(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return;

        //instancia el enemigo
        GameObject enemyInstance = Instantiate(prefab, position, rotation);

        //si estamos en multijugador, spawnea como NetworkObject
        NetworkObject networkObj = enemyInstance.GetComponent<NetworkObject>();
        if (networkObj != null)
        {
            networkObj.Spawn();
            //log siempre (no solo en debugMode) para facilitar diagnóstico de posición
            Debug.Log($"[EnemySpawner] Spawneado <b>{prefab.name}</b> en world {(Vector2)position}");
        }
        else
        {
            Debug.Log($"[EnemySpawner] Spawneado local <b>{prefab.name}</b> en world {(Vector2)position}");
        }
    }

    public void SpawnEnemyType(int typeIndex, Vector3 position)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        GameObject prefab;
        
        if (typeIndex == 0)
            prefab = slimesPrefab;
        else if (typeIndex == 1)
            prefab = enemyflyPrefab;
        else
            prefab = enemyLongSwordKnightPrefab;

        SpawnEnemyAt(prefab, position, Quaternion.identity);
    }
}