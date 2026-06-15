using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class LevelGoal : MonoBehaviour
{
    [Header("Requisito para ganar")]
    public GameObject bossRequerido;

    [Header("Mensaje de Bloqueo (Nota)")]
    public TextMeshProUGUI textoAviso;
    public string mensaje = "¡Debes derrotar al jefe para pasar!";

    [Header("Configuración de Destino")]
    public string nombreSiguienteNivel = "Masmorra_Nivel2"; 
    public bool cargarAlInstante = false;

    private SpriteRenderer spriteRenderer;
    private bool estaDesbloqueada = false;

    private bool hasTriggered = false;

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
        if (!estaDesbloqueada && bossRequerido == null)
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

            //Notifica al GameManager (congela tiempo, muestra pantalla de victoria)
            if (GameManager.instance != null)
                GameManager.instance.WinLevel();

            //RecordSystem persiste entre sesiones y solo sobreescribe si es mejor tiempo.
            if (RecordSystem.instance != null && GameManager.instance != null)
            {
                int sceneIdx   = SceneManager.GetActiveScene().buildIndex;
                string sceneName = SceneManager.GetActiveScene().name;
                bool esRecord  = RecordSystem.instance.TrySetRecord(
                    sceneIdx, sceneName, GameManager.instance.ElapsedTime);

                if (esRecord)
                    Debug.Log($"🏆 ¡Nuevo récord en {sceneName}: {GameManager.instance.GetTimeString()}!");
            }

            //Notifica al CoopManager la victoria compartida de ambos jugadores
            if (CoopManager.instance != null && GameManager.instance != null)
            {
                CoopManager.instance.OnGoalReached(GameManager.instance.ElapsedTime);
            }

            //Detiene físicamente al jugador
            Rigidbody2D playerRb = collision.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector2.zero; 
                playerRb.bodyType       = RigidbodyType2D.Static; 
            }

            if (cargarAlInstante)
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