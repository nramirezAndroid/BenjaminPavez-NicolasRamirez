using UnityEngine;

public class Parallax : MonoBehaviour
{
    [Header("Sensibilidad")]
    public float parallaxMultiplier = 0.02f; 

    private Material parallaxMaterial;
    private Transform camTransform;
    private Vector3 lastCamPosition;

    void Start()
    {
        //Busca el componente Renderer del Quad
        parallaxMaterial = GetComponent<Renderer>().material;
        
        //Busca la cámara principal automáticamente
        camTransform = Camera.main.transform;
        lastCamPosition = camTransform.position;
    }

    void LateUpdate()
    {
        //Calcula cuánto se desplazó la cámara en este frame
        float deltaX = camTransform.position.x - lastCamPosition.x;

        //Mueve la textura una fracción de ese movimiento
        parallaxMaterial.mainTextureOffset += new Vector2(deltaX * parallaxMultiplier, 0);

        //Actualiza la posición para el siguiente cálculo
        lastCamPosition = camTransform.position;
    }
}