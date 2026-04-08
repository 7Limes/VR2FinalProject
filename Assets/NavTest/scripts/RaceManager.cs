using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Added for sorting

public class RaceManager : MonoBehaviour
{
    public static RaceManager Instance;

    [Header("Race Settings")]
    public int totalLaps = 3;
    public bool raceHasStarted = false;

    [Header("UI References")]
    public Image countdownImage;
    public Sprite[] countdownSprites;
    public TextMeshProUGUI resultsText;
    public GameObject resultsPanel;
    public TextMeshProUGUI championStatusText; // Drag your new UI here

    private List<NPCMovement> allRacers = new List<NPCMovement>();
    private List<string> finishedNames = new List<string>();
    private List<string> formattedFinishedResults = new List<string>();

    void Awake() { Instance = this; }

    void Start()
    {
        resultsPanel.SetActive(false);
        // Find every NPC in the scene and add them to our tracking list
        allRacers.AddRange(FindObjectsOfType<NPCMovement>());

        if (countdownSprites.Length > 0) StartCoroutine(StartCountdown());
        else raceHasStarted = true;
    }

    IEnumerator StartCountdown()
    {
        foreach (Sprite sprite in countdownSprites)
        {
            countdownImage.sprite = sprite;
            yield return new WaitForSeconds(1f);
        }
        countdownImage.enabled = false;
        raceHasStarted = true;

        // Initialize everyone to Lap 1
        foreach (var racer in allRacers) racer.currentLap = 1;
    }

    void Update()
    {
        if (raceHasStarted)
        {
            UpdateChampionUI();
        }
    }

    void UpdateChampionUI()
    {
        // 1. Sort all racers to find placements
        // Sorting logic: Highest lap first, then shortest distance to goal
        var sortedRacers = allRacers
            .OrderByDescending(r => r.currentLap)
            .ThenBy(r => r.distanceToGoal)
            .ToList();

        // 2. Find the Champion in that sorted list
        NPCMovement champion = allRacers.Find(r => r.gameObject.name == "Champion");

        if (champion != null)
        {
            int rank = sortedRacers.IndexOf(champion) + 1;
            string suffix = GetOrdinalSuffix(rank);

            championStatusText.text = $"RANK: {rank}{suffix} | LAP: {champion.currentLap}/{totalLaps}";

            // If champion finishes
            if (champion.isFinished) championStatusText.text = "FINISHED!";
        }
    }

    public void PassCheckPoint(NPCMovement racer, Color racerColor)
    {
        if (racer.isFinished) return;

        if (racer.currentLap < totalLaps)
        {
            racer.currentLap++;
            Debug.Log($"{racer.name} started lap {racer.currentLap}");
        }
        else
        {
            // Racer has finished the final lap!
            racer.isFinished = true;
            RecordFinisher(racer.name, racerColor);
        }
    }

    void RecordFinisher(string racerName, Color racerColor)
    {
        if (!finishedNames.Contains(racerName))
        {
            finishedNames.Add(racerName);
            string hexColor = ColorUtility.ToHtmlStringRGB(racerColor);
            formattedFinishedResults.Add($"<color=#{hexColor}>{racerName}</color>");
            UpdateFinalResultsUI();
        }
    }

    void UpdateFinalResultsUI()
    {
        resultsPanel.SetActive(true);
        resultsText.text = "<b>Race Results:</b>\n";
        for (int i = 0; i < finishedNames.Count; i++)
        {
            resultsText.text += $"{i + 1}{GetOrdinalSuffix(i + 1)}: {formattedFinishedResults[i]}\n";
        }
    }

    string GetOrdinalSuffix(int num)
    {
        if (num <= 0) return "";
        switch (num % 100)
        {
            case 11: case 12: case 13: return "th";
        }
        switch (num % 10)
        {
            case 1: return "st";
            case 2: return "nd";
            case 3: return "rd";
            default: return "th";
        }
    }
}