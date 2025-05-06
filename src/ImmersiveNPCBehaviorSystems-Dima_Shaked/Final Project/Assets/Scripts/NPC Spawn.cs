using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class NPCSpawnerWithUI : MonoBehaviour
{
    [Header("NPC Settings")]
    public GameObject npcPrefab;
    public List<Vector3> pointsOfInterest = new List<Vector3>
    {
        new Vector3(-20, 0, -20), new Vector3(-15, 0, -20), new Vector3(-10, 0, -20), new Vector3(-5, 0, -20), new Vector3(0, 0, -20),
        new Vector3(5, 0, -20), new Vector3(10, 0, -20), new Vector3(15, 0, -20), new Vector3(20, 0, -20), new Vector3(-20, 0, -15),
        new Vector3(-15, 0, -15), new Vector3(-10, 0, -15), new Vector3(-5, 0, -15), new Vector3(0, 0, -15), new Vector3(5, 0, -15),
        new Vector3(10, 0, -15), new Vector3(15, 0, -15), new Vector3(20, 0, -15), new Vector3(-20, 0, -10), new Vector3(-15, 0, -10),
        new Vector3(-10, 0, -10), new Vector3(-5, 0, -10), new Vector3(0, 0, -10), new Vector3(5, 0, -10), new Vector3(10, 0, -10),
        new Vector3(15, 0, -10), new Vector3(20, 0, -10), new Vector3(-20, 0, -5), new Vector3(-15, 0, -5), new Vector3(-10, 0, -5),
        new Vector3(-5, 0, -5), new Vector3(0, 0, -5), new Vector3(5, 0, -5), new Vector3(10, 0, -5), new Vector3(15, 0, -5),
        new Vector3(20, 0, -5), new Vector3(-20, 0, 0), new Vector3(-15, 0, 0), new Vector3(-10, 0, 0), new Vector3(-5, 0, 0),
        new Vector3(0, 0, 0), new Vector3(5, 0, 0), new Vector3(10, 0, 0), new Vector3(15, 0, 0), new Vector3(20, 0, 0),
        new Vector3(-20, 0, 5), new Vector3(-15, 0, 5), new Vector3(-10, 0, 5), new Vector3(-5, 0, 5), new Vector3(0, 0, 5),
        new Vector3(5, 0, 5), new Vector3(10, 0, 5), new Vector3(15, 0, 5), new Vector3(20, 0, 5), new Vector3(-20, 0, 10),
        new Vector3(-15, 0, 10), new Vector3(-10, 0, 10), new Vector3(-5, 0, 10), new Vector3(0, 0, 10), new Vector3(5, 0, 10)
    };

    private List<GameObject> spawnedNPCs = new List<GameObject>();

    [Header("UI References")]
    public TMP_InputField npcCountInput;
    public TMP_InputField moveSpeedInput;
    public GameObject menuPanel;

    void Start()
    {
        if (npcPrefab != null)
            npcPrefab.SetActive(false); // Keep prefab hidden at start
    }

    public void OnGenerateButtonClicked()
    {
        int npcCount = 20;
        float moveSpeed = 2f;

        if (int.TryParse(npcCountInput.text, out int parsedCount))
            npcCount = parsedCount;

        if (float.TryParse(moveSpeedInput.text, out float parsedSpeed))
            moveSpeed = parsedSpeed;

        SpawnNPCs(npcCount, moveSpeed);
        menuPanel.SetActive(false); // Hide menu after spawn
    }

    private void SpawnNPCs(int npcCount, float moveSpeed)
    {
        for (int i = 0; i < npcCount; i++)
        {
            Vector3 randomPosition = new Vector3(Random.Range(-10f, 10f), 0, Random.Range(-10f, 10f));
            GameObject npc = Instantiate(npcPrefab, randomPosition, Quaternion.identity);
            npc.SetActive(true);
            NPCMovement npcMovement = npc.AddComponent<NPCMovement>();
            npcMovement.Initialize(pointsOfInterest, moveSpeed);
            spawnedNPCs.Add(npc);
        }
    }
}
