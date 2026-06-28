using UnityEngine;

public class DeathZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        //si cae el Jugador, se muere instantáneamente
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                //se le entrega un numero exagerado de daño para asegurar su muerte
                player.TakeDamage(999, transform); 
                Debug.Log("<color=red>¡El Player cayó al vacío y murió!</color>");
            }
        }
        
        //si un enemigo se cae al vacío, también lo eliminamos
        else if (other.CompareTag("Enemy") || other.GetComponent<EnemyBase>() != null)
        {
            EnemyBase enemy = other.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.TakeDamage(999, transform);
            }
            else
            {
                Destroy(other.gameObject);
            }
        }
    }
}