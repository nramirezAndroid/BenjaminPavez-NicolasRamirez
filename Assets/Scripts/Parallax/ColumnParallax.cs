using UnityEngine;

public class ColumnParallax : MonoBehaviour
{
    [Header("Configuración de Movimiento")]
    public float parallaxMultiplier = 0.02f; 

    [Header("Configuración de Textura")]
    public float densidadX = 1f; 

    private Material columnMaterial;
    private Transform camTransform;
    private Vector3 lastCamPosition;
    private float offset;

    void Start()
    {
        //Obtiene el material del Quad
        columnMaterial = GetComponent<Renderer>().material;
        
        //Ajusta la repetición inicial
        columnMaterial.mainTextureScale = new Vector2(densidadX, 1f);

        //Referencia a la cámara
        if (Camera.main != null) camTransform = Camera.main.transform;
        lastCamPosition = camTransform.position;
    }

    void LateUpdate()
    {
        //Bloqueo por pausa
        if (GameManager.instance != null && GameManager.instance.isPaused) return;
        if (camTransform == null) return;

        float deltaX = camTransform.position.x - lastCamPosition.x;
        offset += deltaX * parallaxMultiplier;
        columnMaterial.mainTextureOffset = new Vector2(offset, 0);

        //Forzamos al objeto físico a seguir a la cámara en X e Y
        transform.position = new Vector3(camTransform.position.x, camTransform.position.y, transform.position.z);

        //Actualizamos la última posición
        lastCamPosition = camTransform.position;
    }
}