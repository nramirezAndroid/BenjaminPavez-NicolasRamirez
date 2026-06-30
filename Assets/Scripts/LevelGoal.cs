using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class LevelGoal : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Requisito para ganar")]
    [Tooltip("Spawn point o instancia del boss. Si apunta a un SpawnPointConfig, " +
             "el nombre del boss se deduce automáticamente y se usa como centro de zona.")]
    [SerializeField] private GameObject bossRequerido;

    [Tooltip("Nombre (o substring) del boss spawneado en runtime. " +
             "Ej: 'EnemyLongSwordKnight' para Level 2, 'bossDemon' para Level 3. " +
             "Si está vacío se deduce del tipo de SpawnPointConfig de bossRequerido.")]
    [SerializeField] private string nombreBoss;

    [Tooltip("Radio en unidades de mundo alrededor de bossRequerido para filtrar " +
             "solo los bosses de ESTA zona. 0 = sin filtro (todos los del nivel). " +
             "Si no se configura y bossRequerido tiene SpawnPointConfig, se usa 30.")]
    [SerializeField] private float radioZonaBoss = 0f;

    [Header("Mensaje de Bloqueo")]
    [SerializeField] private TextMeshProUGUI textoAviso;
    [SerializeField] private string mensaje;

    [Header("Configuración de Destino")]
    [SerializeField] private string nombreSiguienteNivel;
    [SerializeField] private bool cargarAlInstante;

    [Header("Victoria Final (Cooperativo)")]
    [Tooltip("Marcar SOLO en el LevelGoal del último nivel. " +
             "Niveles intermedios simplemente cargan el siguiente sin panel de victoria.")]
    [SerializeField] private bool esVictoriaFinal;

    // ─────────────────────────────────────────────────────────────────────────
    // Estado interno
    // ─────────────────────────────────────────────────────────────────────────

    private SpriteRenderer   spriteRenderer;
    private bool             estaDesbloqueada  = false;
    private bool             hasTriggered      = false;
    private bool             playerEnTrigger   = false;
    private float            checkBossTimer    = 0f;

    // Rastreo de bosses descubiertos en ESTA zona (evita confundir con otros del nivel)
    private HashSet<EnemyBase> bossesDeLaZona = new HashSet<EnemyBase>();

    // Radio efectivo de zona (calculado en Start)
    private float radioEfectivo = 0f;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity
    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // ── Configurar textoAviso ────────────────────────────────────────────
        if (textoAviso != null)
        {
            // Desactivar de entrada
            textoAviso.gameObject.SetActive(false);

            // Corregir wrapping vertical: el text box puede ser demasiado estrecho
            // para el mensaje con la fuente configurada.
            textoAviso.enableWordWrapping = false;
            textoAviso.overflowMode       = TextOverflowModes.Overflow;
            textoAviso.fontSize           = 26f;

            // Ampliar el rect para que quepa el texto en una sola línea
            RectTransform rt = textoAviso.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(520, 60);
        }

        // ── Deducir nombreBoss desde SpawnPointConfig si no está configurado ──
        if (string.IsNullOrEmpty(nombreBoss) && bossRequerido != null)
        {
            SpawnPointConfig config = bossRequerido.GetComponent<SpawnPointConfig>();
            if (config != null)
            {
                nombreBoss = NombreBossDesdeEnemyType(config.GetEnemyType());
                if (!string.IsNullOrEmpty(nombreBoss))
                    Debug.Log($"[LevelGoal] '{gameObject.name}' nombreBoss auto: '{nombreBoss}'");
            }
        }

        // ── Calcular radio efectivo de zona ──────────────────────────────────
        // Si el usuario dejó 0 pero hay un SpawnPointConfig, usamos 30 como defecto.
        if (radioZonaBoss > 0f)
            radioEfectivo = radioZonaBoss;
        else if (bossRequerido != null && bossRequerido.GetComponent<SpawnPointConfig>() != null)
            radioEfectivo = 30f;
        else
            radioEfectivo = 0f; // sin filtro de zona

        // ── Estado inicial ───────────────────────────────────────────────────
        bool tieneRequisito = !string.IsNullOrEmpty(nombreBoss) || bossRequerido != null;

        if (!tieneRequisito)
            DesbloquearMeta();
        else
            if (spriteRenderer != null) spriteRenderer.color = new Color(1f, 1f, 1f, 0.3f);
    }

    void Update()
    {
        // ── Ocultar aviso mientras el juego está pausado ─────────────────────
        bool juegoEnPausa = GameManager.instance != null && GameManager.instance.IsPaused;
        if (juegoEnPausa)
        {
            if (textoAviso != null && textoAviso.gameObject.activeSelf)
                textoAviso.gameObject.SetActive(false);
            return;
        }

        // Si estaba en pausa y el jugador sigue dentro del trigger bloqueado, volver a mostrar
        if (playerEnTrigger && !estaDesbloqueada && textoAviso != null
            && !textoAviso.gameObject.activeSelf)
        {
            textoAviso.text = mensaje;
            textoAviso.gameObject.SetActive(true);
        }

        if (estaDesbloqueada) return;

        bool tieneRequisito = !string.IsNullOrEmpty(nombreBoss) || bossRequerido != null;

        if (!tieneRequisito)
        {
            DesbloquearMeta();
            return;
        }

        // Revisar cada 0.5 s
        checkBossTimer += Time.deltaTime;
        if (checkBossTimer < 0.5f) return;
        checkBossTimer = 0f;

        // ── Caso A: búsqueda por nombre con filtro de zona ───────────────────
        if (!string.IsNullOrEmpty(nombreBoss))
        {
            // Fase 1: descubrir bosses nuevos que pertenezcan a esta zona
            foreach (EnemyBase enemy in FindObjectsByType<EnemyBase>(
                UnityEngine.FindObjectsInactive.Exclude,
                UnityEngine.FindObjectsSortMode.None))
            {
                if (bossesDeLaZona.Contains(enemy)) continue;
                if (!enemy.gameObject.name.Contains(nombreBoss)) continue;

                bool enZona = radioEfectivo <= 0f || bossRequerido == null ||
                    Vector2.Distance(enemy.transform.position,
                                     bossRequerido.transform.position) <= radioEfectivo;

                if (enZona)
                {
                    bossesDeLaZona.Add(enemy);
                    Debug.Log($"[LevelGoal] '{gameObject.name}' descubrió boss en zona: " +
                              $"'{enemy.gameObject.name}' dist=" +
                              (bossRequerido != null
                                  ? Vector2.Distance(enemy.transform.position,
                                                     bossRequerido.transform.position).ToString("F1")
                                  : "n/a"));
                }
            }

            // Mientras no haya ninguno descubierto, esperar (EnemySpawner aún no terminó)
            if (bossesDeLaZona.Count == 0) return;

            // Fase 2: ¿queda alguno vivo o sin destruir?
            bool hayVivoEnZona = false;
            foreach (EnemyBase boss in bossesDeLaZona)
            {
                // null → NGO despawneó el objeto; isDead → murió y aún no fue despawneado
                if (boss != null && !boss.IsDead)
                {
                    hayVivoEnZona = true;
                    break;
                }
            }

            if (!hayVivoEnZona)
                DesbloquearMeta();

            return;
        }

        // ── Caso B: bossRequerido fue destruido por NGO ───────────────────────
        if (bossRequerido == null)
        {
            DesbloquearMeta();
            return;
        }

        // ── Caso C: bossRequerido tiene EnemyBase directa ────────────────────
        EnemyBase bossEnemy = bossRequerido.GetComponent<EnemyBase>();
        if (bossEnemy != null)
        {
            if (bossEnemy.IsDead) DesbloquearMeta();
            return;
        }

        Debug.LogWarning($"[LevelGoal] '{gameObject.name}': bossRequerido no tiene " +
                         "EnemyBase ni SpawnPointConfig reconocible. " +
                         "Configura 'nombreBoss' manualmente.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string NombreBossDesdeEnemyType(SpawnPointConfig.EnemyType tipo)
    {
        switch (tipo)
        {
            case SpawnPointConfig.EnemyType.Knight:    return "EnemyLongSwordKnight";
            case SpawnPointConfig.EnemyType.BossDemon: return "bossDemon";
            case SpawnPointConfig.EnemyType.Slimes:    return "Slimes";
            case SpawnPointConfig.EnemyType.Enemyfly:  return "Enemyfly";
            default: return null;
        }
    }

    private void DesbloquearMeta()
    {
        estaDesbloqueada = true;
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
        if (textoAviso     != null) textoAviso.gameObject.SetActive(false);
        Debug.Log($"[LevelGoal] '{gameObject.name}' DESBLOQUEADA.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Triggers
    // ─────────────────────────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;

        if (estaDesbloqueada)
        {
            if (hasTriggered) return;
            hasTriggered = true;

            // Caso primario: el menú seteó el modo correctamente.
            // Fallback: si NGO está activo como cliente puro (IsClient && !IsHost),
            // estamos en coop aunque modoSeleccionado sea Ninguno (p.ej. en pruebas de editor
            // que cargan la escena directamente sin pasar por el menú en la máquina del CLIENT).
            bool esCooperativo = NetworkModeData.modoSeleccionado == NetworkModeData.Mode.Host
                              || NetworkModeData.modoSeleccionado == NetworkModeData.Mode.Cliente
                              || (Unity.Netcode.NetworkManager.Singleton != null
                                  && Unity.Netcode.NetworkManager.Singleton.IsClient
                                  && !Unity.Netcode.NetworkManager.Singleton.IsHost);

            if (esCooperativo)
            {
                bool esServidor = Unity.Netcode.NetworkManager.Singleton != null
                               && Unity.Netcode.NetworkManager.Singleton.IsServer;

                Debug.Log($"[LevelGoal] '{gameObject.name}' Coop — esServidor={esServidor}, " +
                          $"siguienteNivel='{nombreSiguienteNivel}', esVictoriaFinal={esVictoriaFinal}");

                if (esServidor && CoopManager.instance != null)
                {
                    float tiempo = GameManager.instance != null ? GameManager.instance.ElapsedTime : 0f;
                    CoopManager.instance.OnGoalReached(tiempo, esVictoriaFinal, nombreSiguienteNivel);
                }
            }
            else
            {
                // Modo Solitario
                if (GameManager.instance != null)
                    GameManager.instance.WinLevel();
            }

            // Detener al jugador solo en solitario
            if (!esCooperativo)
            {
                Rigidbody2D playerRb = collision.GetComponent<Rigidbody2D>();
                if (playerRb != null)
                {
                    playerRb.linearVelocity = Vector2.zero;
                    playerRb.bodyType       = RigidbodyType2D.Static;
                }
            }

            // cargarAlInstante: aplica solo en solitario
            if (!esCooperativo && cargarAlInstante)
            {
                Time.timeScale = 1f;
                if (LoadingScreenManager.Instance != null)
                    LoadingScreenManager.Instance.LoadScene(nombreSiguienteNivel);
                else
                    SceneManager.LoadScene(nombreSiguienteNivel);
            }
        }
        else
        {
            // Bloqueada: mostrar aviso
            playerEnTrigger = true;
            if (textoAviso != null)
            {
                textoAviso.text = mensaje;
                textoAviso.gameObject.SetActive(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;
        playerEnTrigger = false;
        if (textoAviso != null) textoAviso.gameObject.SetActive(false);
    }
}
