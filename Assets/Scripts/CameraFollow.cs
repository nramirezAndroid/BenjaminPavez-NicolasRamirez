using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Objetivo")]
    //public: PlayerController y Player2Controller asignan el target en runtime
    public Transform target;

    [Header("Configuración de Cámara")]
    [SerializeField] private float smoothTime;

    public Vector3 offset = new Vector3(0f, 1f, -10f);

    private Vector3 velocity = Vector3.zero;

    //usamos LateUpdate para la cámara
    //esto asegura que el jugador se mueva primero en Update/FixedUpdate, y la cámara lo siga después, evitando tirones.
    void LateUpdate()
    {
        //si no hay objetivo asignado, no hacemos nada para evitar errores
        if (target == null) return;

        //calculamos la posición a la que queremos que vaya la cámara
        Vector3 targetPosition = target.position + offset;

        //movemos la cámara suavemente desde su posición actual hacia la posición objetivo
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
    }

    //Teletransporta la cámara inmediatamente al target actual, sin transición suave.
    //Úsalo al cambiar de escena o al asignar un nuevo target para evitar que SmoothDamp
    //interpole desde la posición de la escena anterior (por ejemplo, del bosque a la mazmorra).
    public void SnapToTarget()
    {
        if (target == null) return;
        velocity = Vector3.zero;
        transform.position = target.position + offset;
        Debug.Log($"[CameraFollow] Snap a {target.name} en {transform.position}");
    }
}