using UnityEngine;

public class DestroyVFX : MonoBehaviour
{
    [Tooltip("Tiempo en segundos antes de borrar el tajo de la escena")]
    [SerializeField] private float lifeTime = 0.35f; 

    void Start()
    {
        //Se elimina automáticamente del juego tras expirar el tiempo de la animación
        Destroy(gameObject, lifeTime);
    }
}