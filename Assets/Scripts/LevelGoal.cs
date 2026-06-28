using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class LevelGoal : MonoBehaviour
{
    [Header("Requisito para ganar")]
    [SerializeField] private GameObject bossRequerido;

    [Header("Mensaje de Bloqueo (Nota)")]
    [SerializeField] private TextMeshProUGUI textoAviso;
    [SerializeField] private string mensaje;

    [Header("Configuración de Destino")]
    [SerializeField] private string nombreSiguienteNivel;
    [SerializeField] private bool cargarAlInstante;

    [Header("Victoria Final (Cooperativo)")]
    [Tooltip("Marca esto SOLO en el LevelGoal del último nivel. " +
             "Los niveles intermedios cargan el siguiente nivel sin mostrar el panel de victoria.")]
    [SerializeField] private bool esVictoriaFinal;

    private SpriteRenderer spriteRenderer;
    private bool estaDesbloqueada = false;
    private bool hasTriggered = false;
    private float checkBossTimer = 0f;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (textoAviso != null) textoAviso.gameObject.SetActive(false);

        if (bossRequerido == null)
            DesbloquearMeta();
        else
            if (spriteRenderer != null) spriteRenderer.color = new Color(1f, 1f, 1f, 0.3f);
    }

    void Update()
    {
        if (estaDesbloqueada) return;

        //sin requisito de jefe → siempre desbloqueada
        if (bossRequerido == null)
        {
            DesbloquearMeta();
            return;
        }

        //con requisito: revisar cada 0.5 s para no saturar FindObjectsByType
        checkBossTimer += Time.deltaTime;
        if (checkBossTimer < 0.5f) return;
        checkBossTimer = 0f;

        //caso 1: el jefe es la instancia directa y fue destruida/despawneada
        EnemyBase bossEnemy = bossRequerido.GetComponent<EnemyBase>();
        if (bossEnemy == null || bossEnemy.IsDead)
        {
            DesbloquearMeta();
            return;
        }

        //caso 2: bossRequerido apunta al prefab (nunca null), o el jefe no se destruye.
        //buscamos si queda algún EnemyLongSwordKnight vivo en la escena.
        foreach (EnemyLongSwordKnight vivo in
            FindObjectsByType<EnemyLongSwordKnight>(
                UnityEngine.FindObjectsInactive.Exclude,
                UnityEngine.FindObjectsSortMode.None))
        {
            if (!vivo.IsDead) return; //hay uno vivo → seguir esperando
        }

        //ninguno vivo (o ninguno en escena) → desbloquear
        DesbloquearMeta();
    }

    private void DesbloquearMeta()
    {
        estaDesbloqueada = true;
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
        if (textoAviso != null) textoAviso.gameObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;

        if (estaDesbloqueada)
        {
            if (hasTriggered) return;
            hasTriggered = true;

            bool esCooperativo = NetworkModeData.modoSeleccionado == NetworkModeData.Mode.Host
                              || NetworkModeData.modoSeleccionado == NetworkModeData.Mode.Cliente;

            if (esCooperativo)
            {
                //modo Cooperativo: SOLO el servidor decide cuándo avanzar.
                //el trigger puede dispararse en ambas máquinas (física local de NGO),
                //pero solo el servidor debe llamar OnGoalReached para evitar RPCs duplicados.
                bool esServidor = Unity.Netcode.NetworkManager.Singleton != null
                               && Unity.Netcode.NetworkManager.Singleton.IsServer;

                //log de diagnóstico: permite ver exactamente qué condición falla en consola
                Debug.Log($"[LevelGoal] Coop trigger — esServidor={esServidor}, " +
                          $"CoopManager={CoopManager.instance != null}, " +
                          $"GameManager={GameManager.instance != null}, " +
                          $"siguienteNivel='{nombreSiguienteNivel}', " +
                          $"esVictoriaFinal={esVictoriaFinal}");

                if (esServidor && CoopManager.instance != null)
                {
                    //GameManager puede no estar presente en modo coop; usamos 0f como fallback
                    float tiempo = GameManager.instance != null ? GameManager.instance.ElapsedTime : 0f;
                    CoopManager.instance.OnGoalReached(tiempo, esVictoriaFinal, nombreSiguienteNivel);
                }
            }
            else
            {
                //modo Solitario: comportamiento original.
                if (GameManager.instance != null)
                    GameManager.instance.WinLevel();
            }

            //detiene físicamente al jugador.
            //en cooperativo NO se congela: Rigidbody.Static causaría errores "Cannot use linearVelocity"
            //mientras la transición de escena está en curso. CoopManager maneja el cambio de nivel.
            if (!esCooperativo)
            {
                Rigidbody2D playerRb = collision.GetComponent<Rigidbody2D>();
                if (playerRb != null)
                {
                    playerRb.linearVelocity = Vector2.zero;
                    playerRb.bodyType       = RigidbodyType2D.Static;
                }
            }

            //cargarAlInstante solo aplica al modo Solitario.
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
            if (textoAviso != null)
            {
                textoAviso.text = mensaje;
                textoAviso.gameObject.SetActive(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
            if (textoAviso != null) textoAviso.gameObject.SetActive(false);
    }
}