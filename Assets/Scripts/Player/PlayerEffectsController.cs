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
            PlayAttackSlashAt(attackPoint.position, isFacingRight);
    }

    //versión con posición explícita — usada por el ClientRpc para reproducir el VFX en P2
    public void PlayAttackSlashAt(Vector3 position, bool isFacingRight)
    {
        if (tajoVfxPrefab == null) return;

        GameObject tajoInstance = Instantiate(tajoVfxPrefab, position, Quaternion.identity);
        Vector3 escalaTajo = tajoInstance.transform.localScale;
        escalaTajo.x = isFacingRight ? Mathf.Abs(escalaTajo.x) : -Mathf.Abs(escalaTajo.x);
        tajoInstance.transform.localScale = escalaTajo;
    }
}