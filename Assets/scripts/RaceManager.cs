using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class RaceManager : MonoBehaviour
{
    public static RaceManager Instance;

    [Header("Countdown Settings")]
    public Image countdownImage;
    [Tooltip("Drag your custom sprites here in order: 3, 2, 1, GO")]
    public Sprite[] countdownSprites;
    public float timeBetweenCounts = 1f;
    public bool raceHasStarted = false;

    [Header("Results Settings")]
    public GameObject resultsPanel;
    public TextMeshProUGUI resultsText;

    private List<string> finishedNPCs = new List<string>();

    void Awake()
    {
        // this makes the RaceManager a Singleton, meaning any script can easily access it
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        resultsPanel.SetActive(false);
        resultsText.text = "<b>Race Results:</b>\n";

        if (countdownSprites.Length > 0)
        {
            StartCoroutine(StartCountdown());
        }
        else
        {
            Debug.LogWarning("No countdown sprites assigned!");
            raceHasStarted = true;
        }
    }

    IEnumerator StartCountdown()
    {
        // loop through the custom assets
        foreach (Sprite sprite in countdownSprites)
        {
            countdownImage.sprite = sprite;
            yield return new WaitForSeconds(timeBetweenCounts);
        }

        // hide the image and start the race
        countdownImage.enabled = false;
        raceHasStarted = true;
    }

    public void RecordFinisher(string racerName)
    {
        // make sure we don't count the same npc twice if they bump the finish line again
        if (!finishedNPCs.Contains(racerName))
        {
            finishedNPCs.Add(racerName);
            UpdateResultsUI();
        }
    }

    void UpdateResultsUI()
    {
        resultsPanel.SetActive(true);

        int rank = finishedNPCs.Count;
        string suffix = "th";

        if (rank == 1) suffix = "st";
        else if (rank == 2) suffix = "nd";
        else if (rank == 3) suffix = "rd";

        // append the new finisher to the text block
        resultsText.text += $"{rank}{suffix}: {finishedNPCs[rank - 1]}\n";
    }
}