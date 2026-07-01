using UnityEngine;

public class ColumnParallax : MonoBehaviour
{
    [Header("Configuración de Movimiento")]
    [SerializeField] private float parallaxMultiplier;

    [Header("Configuración de Textura")]
    [SerializeField] private float densidadX;

    private Material columnMaterial;
    private Transform camTransform;
    private Vector3 lastCamPosition;
    private float offset;

    void Start()
    {
        //obtiene el material del Quad
        columnMaterial = GetComponent<Renderer>().material;
        
        //ajusta la repetición inicial
        columnMaterial.mainTextureScale = new Vector2(densidadX, 1f);

        //referencia a la cámara
        if (Camera.main != null) camTransform = Camera.main.transform;
        lastCamPosition = camTransform.position;
    }

    void LateUpdate()
    {
        //bloqueo por pausa
        if (GameManager.instance != null && GameManager.instance.IsPaused) return;
        if (camTransform == null) return;

        float deltaX = camTransform.position.x - lastCamPosition.x;
        offset += deltaX * parallaxMultiplier;
        columnMaterial.mainTextureOffset = new Vector2(offset, 0);

        //forzamos al objeto físico a seguir a la cámara en X e Y
        transform.position = new Vector3(camTransform.position.x, camTransform.position.y, transform.position.z);

        //actualizamos la última posición
        lastCamPosition = camTransform.position;
    }
}