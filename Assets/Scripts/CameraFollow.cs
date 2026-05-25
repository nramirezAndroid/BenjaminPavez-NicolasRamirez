using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Objetivo")]
    public Transform target;

    [Header("Configuración de Cámara")]
    public float smoothTime = 0.2f;

    public Vector3 offset = new Vector3(0f, 1f, -10f);

    private Vector3 velocity = Vector3.zero;

    //Usamos LateUpdate para la cámara
    //Esto asegura que el jugador se mueva primero en Update/FixedUpdate, y la cámara lo siga después, evitando tirones.
    void LateUpdate()
    {
        //Si no hay objetivo asignado, no hacemos nada para evitar errores
        if (target == null) return;

        //Calculamos la posición a la que queremos que vaya la cámara
        Vector3 targetPosition = target.position + offset;

        //Movemos la cámara suavemente desde su posición actual hacia la posición objetivo
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
    }
}