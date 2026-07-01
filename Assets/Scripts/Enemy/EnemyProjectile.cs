using UnityEngine;
using Unity.Netcode;

public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] private float speed;
    [SerializeField] private int damage;
    [SerializeField] private float lifeTime;

    private Rigidbody2D rb;
    private NetworkObject networkObj;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        networkObj = GetComponent<NetworkObject>();
        
        if (rb != null)
        {
            rb.linearVelocity = transform.right * speed;
        }

        //auto-destrucción después de lifeTime
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        //si impacta al jugador
        if (collision.CompareTag("Player"))
        {
            PlayerController player = collision.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(damage, transform);
            }
            DestroyProjectile();
            return;
        }
        
        //si impacta una pared
        if (collision.gameObject.layer == LayerMask.NameToLayer("Pared"))
        {
            DestroyProjectile();
            return;
        }
    }

    void DestroyProjectile()
    {
        //en multiplayer, el servidor lo destruye
        if (networkObj != null)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                networkObj.Despawn();
            }
        }
        else
        {
            //en solitario, destrucción normal
            Destroy(gameObject);
        }
    }
}