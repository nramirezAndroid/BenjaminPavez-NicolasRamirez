using UnityEngine;

public class PlayerEffectsController : MonoBehaviour
{
    [Header("VFX de Partículas (Salto)")]
    [SerializeField] private ParticleSystem particulaSalto;

    [Header("VFX de Sprites (Ataque)")]
    [SerializeField] private GameObject tajoVfxPrefab;
    
    [SerializeField] private Transform attackPoint;

    public void PlayJumpDust()
    {
        if (particulaSalto != null)
        {
            particulaSalto.Play();
        }
    }

    public void PlayAttackSlash(bool isFacingRight)
    {
        if (tajoVfxPrefab != null && attackPoint != null)
        {
            //Instancia el tajo en la punta del ataque
            GameObject tajoInstance = Instantiate(tajoVfxPrefab, attackPoint.position, Quaternion.identity);
            
            //Voltea el sprite del tajo si el jugador mira a la izquierda
            Vector3 escalaTajo = tajoInstance.transform.localScale;
            escalaTajo.x = isFacingRight ? Mathf.Abs(escalaTajo.x) : -Mathf.Abs(escalaTajo.x);
            tajoInstance.transform.localScale = escalaTajo;
        }
    }
}