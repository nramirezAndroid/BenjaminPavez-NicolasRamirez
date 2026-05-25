using UnityEngine;

public class DeathZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        //Si cae el Jugador, se muere instantáneamente
        if (other.CompareTag("Player"))
        {
            PlayerControllerComplete player = other.GetComponent<PlayerControllerComplete>();
            if (player != null)
            {
                //Se le entrega un numero exagerado de daño para asegurar su muerte
                player.TakeDamage(999, transform); 
                Debug.Log("<color=red>¡El Player cayó al vacío y murió!</color>");
            }
        }
        
        //Si un enemigo se cae al vacío, también lo eliminamos
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