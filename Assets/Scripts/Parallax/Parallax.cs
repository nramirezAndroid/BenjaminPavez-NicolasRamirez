using UnityEngine;

public class Parallax : MonoBehaviour
{
    [Header("Sensibilidad")]
    [SerializeField] private float parallaxMultiplier;

    private Material parallaxMaterial;
    private Transform camTransform;
    private Vector3 lastCamPosition;

    void Start()
    {
        //busca el componente Renderer del Quad
        parallaxMaterial = GetComponent<Renderer>().material;
        
        //busca la cámara principal automáticamente
        camTransform = Camera.main.transform;
        lastCamPosition = camTransform.position;
    }

    void LateUpdate()
    {
        //calcula cuánto se desplazó la cámara en este frame
        float deltaX = camTransform.position.x - lastCamPosition.x;

        //mueve la textura una fracción de ese movimiento
        parallaxMaterial.mainTextureOffset += new Vector2(deltaX * parallaxMultiplier, 0);

        //actualiza la posición para el siguiente cálculo
        lastCamPosition = camTransform.position;
    }
}