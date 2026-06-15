using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections;


public class Player2Controller : NetworkBehaviour
{


    [Header("Trampas disponibles")]
    public GameObject[] trapPrefabs;
    public LayerMask placementLayer;
    public int trapBudget = 5;



    [Header("Posesión de enemigos")]
    public float possessionRange = 3f;
    public LayerMask enemyLayer;



    [Header("Buffos cooperativos")]
    [Tooltip("Multiplicador de velocidad de P1 durante el buffo")]
    public float speedBuffMultiplier = 1.6f;
    [Tooltip("Multiplicador de daño de P1 durante el buffo")]
    public float damageBuffMultiplier = 2f;
    [Tooltip("Cantidad de vida que se restaura con el buffo de curación")]
    public int healAmount = 30;
    [Tooltip("Duración en segundos de los buffos de velocidad y daño")]
    public float buffDuration = 8f;
    [Tooltip("Tiempo de recarga entre buffos del mismo tipo (segundos)")]
    public float buffCooldown = 20f;

    //Tiempos de recarga individuales por tipo de buffo
    private float cooldownSpeed  = 0f;
    private float cooldownDamage = 0f;
    private float cooldownHeal   = 0f;



    [Header("UI")]
    public TextMeshProUGUI budgetText;
    public GameObject placementGhost;

    [Header("UI de Buffos (iconos/cooldowns)")]
    public TextMeshProUGUI cdSpeedText;
    public TextMeshProUGUI cdDamageText;
    public TextMeshProUGUI cdHealText;



    public bool isP2Active = false;

    public NetworkVariable<int> networkTrapsPlaced = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private int selectedTrap = 0;
    private EnemyBase possessedEnemy = null;
    private Camera cam;




    private PlayerControllerComplete player1 = null;



    void Start()
    {
        cam = Camera.main;
    }

    public override void OnNetworkSpawn()
    {
        networkTrapsPlaced.OnValueChanged += (oldVal, newVal) => UpdateUI();
        UpdateUI();
    }

    public override void OnNetworkDespawn()
    {
        networkTrapsPlaced.OnValueChanged -= (oldVal, newVal) => UpdateUI();
    }

    void Update()
    {
        if (!isP2Active || !IsOwner) return;
        if (Time.timeScale == 0f) return;

        TickCooldowns();
        UpdateCooldownUI();

        HandleTrapPlacement();
        HandleEnemyPossession();
        HandleTrapSelection();
        HandleBuffInputs();
    }



    void HandleTrapPlacement()
    {
        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        if (placementGhost != null)
            placementGhost.transform.position = mouseWorld;

        if (Input.GetMouseButtonDown(0) && networkTrapsPlaced.Value < trapBudget && trapPrefabs.Length > 0)
        {
            RaycastHit2D hit = Physics2D.Raycast(mouseWorld, Vector2.down, 1f, placementLayer);
            if (hit.collider != null)
                PlaceTrapServerRpc(selectedTrap, mouseWorld);
        }
    }

    [ServerRpc]
    public void PlaceTrapServerRpc(int trapIndex, Vector3 spawnPosition)
    {
        if (networkTrapsPlaced.Value >= trapBudget) return;

        GameObject trapInstance = Instantiate(trapPrefabs[trapIndex], spawnPosition, Quaternion.identity);
        trapInstance.GetComponent<NetworkObject>().Spawn();
        networkTrapsPlaced.Value++;
    }

    void HandleTrapSelection()
    {
        if (trapPrefabs.Length <= 1) return;
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0) selectedTrap = (selectedTrap + 1) % trapPrefabs.Length;
        if (scroll < 0) selectedTrap = (selectedTrap - 1 + trapPrefabs.Length) % trapPrefabs.Length;
    }



    void HandleEnemyPossession()
    {
        //Movimiento del enemigo poseído
        if (possessedEnemy != null)
        {
            float h = Input.GetAxisRaw("Horizontal");
            possessedEnemy.MoveAsPossessed(h);

            //Ataque del enemigo: clic izquierdo (si no estamos colocando trampa)
            //Para no interferir con el placement, el clic derecho libera la posesión
        }

        if (!Input.GetMouseButtonDown(1)) return;

        // Clic derecho: poseer / liberar
        if (possessedEnemy != null)
        {
            possessedEnemy.SetPossessed(false);
            possessedEnemy = null;
            return;
        }

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;
        Collider2D hit = Physics2D.OverlapCircle(mouseWorld, possessionRange, enemyLayer);
        if (hit != null)
        {
            EnemyBase enemy = hit.GetComponent<EnemyBase>();
            if (enemy != null && !enemy.isDead)
            {
                possessedEnemy = enemy;
                enemy.SetPossessed(true);
            }
        }
    }

  
    //Buffo cooperativo
    //Teclas: Q = Velocidad | E = Daño | R = Curación


    void HandleBuffInputs()
    {
        if (Input.GetKeyDown(KeyCode.Q) && cooldownSpeed <= 0f)
            RequestBuffServerRpc(BuffType.Speed);

        if (Input.GetKeyDown(KeyCode.E) && cooldownDamage <= 0f)
            RequestBuffServerRpc(BuffType.Damage);

        if (Input.GetKeyDown(KeyCode.R) && cooldownHeal <= 0f)
            RequestBuffServerRpc(BuffType.Heal);
    }

    public enum BuffType { Speed, Damage, Heal }

    [ServerRpc]
    private void RequestBuffServerRpc(BuffType type)
    {
        //El servidor aplica el buffo y confirma a todos los clientes
        ApplyBuffClientRpc(type);
    }

    [ClientRpc]
    private void ApplyBuffClientRpc(BuffType type)
    {
        //Buscamos al P1 si aún no lo tenemos
        if (player1 == null)
            player1 = FindAnyObjectByType<PlayerControllerComplete>();

        if (player1 == null)
        {
            Debug.LogWarning("[Player2Controller] No se encontró PlayerControllerComplete para aplicar buffo.");
            return;
        }

        switch (type)
        {
            case BuffType.Speed:
                player1.ApplySpeedBuff(speedBuffMultiplier, buffDuration);
                //Iniciamos cooldown local en el cliente P2
                if (IsOwner) cooldownSpeed = buffCooldown;
                break;

            case BuffType.Damage:
                player1.ApplyDamageBuff(damageBuffMultiplier, buffDuration);
                if (IsOwner) cooldownDamage = buffCooldown;
                break;

            case BuffType.Heal:
                player1.Heal(healAmount);
                if (IsOwner) cooldownHeal = buffCooldown;
                break;
        }
    }


    void TickCooldowns()
    {
        if (cooldownSpeed  > 0f) cooldownSpeed  -= Time.deltaTime;
        if (cooldownDamage > 0f) cooldownDamage -= Time.deltaTime;
        if (cooldownHeal   > 0f) cooldownHeal   -= Time.deltaTime;
    }

    void UpdateCooldownUI()
    {
        if (cdSpeedText  != null)
            cdSpeedText.text  = cooldownSpeed  > 0f ? $"Q ({cooldownSpeed:F0}s)"  : "Q listo";
        if (cdDamageText != null)
            cdDamageText.text = cooldownDamage > 0f ? $"E ({cooldownDamage:F0}s)" : "E listo";
        if (cdHealText   != null)
            cdHealText.text   = cooldownHeal   > 0f ? $"R ({cooldownHeal:F0}s)"   : "R listo";
    }

    void UpdateUI()
    {
        if (!IsOwner) return;
        if (budgetText != null)
            budgetText.text = $"Trampas: {trapBudget - networkTrapsPlaced.Value} restantes";
    }


    public void ResetForNewRound()
    {
        if (IsServer) networkTrapsPlaced.Value = 0;

        possessedEnemy = null;
        isP2Active     = false;
        cooldownSpeed  = 0f;
        cooldownDamage = 0f;
        cooldownHeal   = 0f;
        UpdateUI();
    }
}
