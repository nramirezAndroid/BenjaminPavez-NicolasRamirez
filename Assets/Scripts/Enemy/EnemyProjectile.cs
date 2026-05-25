using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    public float speed = 8f;
    public int damage = 15;
    public float lifeTime = 4f; //Tiempo antes de autodestruirse para no colapsar la RAM

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        
        //Al instanciarse, calcula su dirección frontal local en base al ángulo que le dio el ángel
        if (rb != null)
        {
            rb.linearVelocity = transform.right * speed;
        }

        //Destrucción automática por tiempo por si sale del mapa
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        //Si impacta al jugador, le aplica daño
        if (collision.CompareTag("Player"))
        {
            PlayerControllerComplete player = collision.GetComponent<PlayerControllerComplete>();
            if (player != null)
            {
                player.TakeDamage(damage, transform);
            }
            Destroy(gameObject); //Destruye la bala al impactar
        }
        
        if (collision.gameObject.layer == LayerMask.NameToLayer("Pared"))
        {
            Destroy(gameObject);
        }
    }
}