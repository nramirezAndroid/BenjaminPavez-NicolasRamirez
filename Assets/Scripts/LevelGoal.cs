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

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        //Asegura que la nota esté oculta al iniciar
        if (textoAviso != null) textoAviso.gameObject.SetActive(false);

        if (bossRequerido == null)
        {
            DesbloquearMeta();
        }
        else
        {
            if (spriteRenderer != null) spriteRenderer.color = new Color(1f, 1f, 1f, 0.3f);
        }
    }

    void Update()
    {
        if (!estaDesbloqueada && bossRequerido == null)
        {
            DesbloquearMeta();
        }
    }

    private void DesbloquearMeta()
    {
        estaDesbloqueada = true;
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
        
        //Si el jugador estaba tocando la meta justo cuando el jefe muere, ses oculta el aviso
        if (textoAviso != null) textoAviso.gameObject.SetActive(false);
    }

 
    //Se activa al entrar a la meta
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if (estaDesbloqueada)
            {
                if (GameManager.instance != null)
                {
                    GameManager.instance.WinLevel();
                }

                Rigidbody2D playerRb = collision.GetComponent<Rigidbody2D>();
                if (playerRb != null)
                {
                    playerRb.linearVelocity = Vector2.zero; 
                    playerRb.bodyType = RigidbodyType2D.Static; 
                }

                if (cargarAlInstante)
                {
                    Time.timeScale = 1f; 
                    if (LoadingScreenManager.Instance != null)
                    {
                        LoadingScreenManager.Instance.LoadScene(nombreSiguienteNivel);
                    }
                    else
                    {
                        SceneManager.LoadScene(nombreSiguienteNivel);
                    }
                }
            }
            else
            {
                //Si la meta esta bloqueada muestra la nota
                if (textoAviso != null)
                {
                    textoAviso.text = mensaje;
                    textoAviso.gameObject.SetActive(true);
                }
            }
        }
    }

    //Se activa al salir de la meta
    private void OnTriggerExit2D(Collider2D collision)
    {

        if (collision.CompareTag("Player"))
        {
            //Oculta la nota
            if (textoAviso != null)
            {
                textoAviso.gameObject.SetActive(false);
            }
        }
    }
}