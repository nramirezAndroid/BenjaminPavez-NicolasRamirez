using UnityEngine;

public class SpawnPointConfig : MonoBehaviour
{
    public enum EnemyType
    {
        Slimes,
        Enemyfly,
        Knight
    }

    [Header("Tipo de Enemigo a Spawnear")]
    [SerializeField] private EnemyType enemyType;

    //método auxiliar para obtener el tipo
    public EnemyType GetEnemyType()
    {
        return enemyType;
    }
}
