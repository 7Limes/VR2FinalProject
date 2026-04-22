using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class RaceManager : MonoBehaviour
{
    public static RaceManager Instance;

    [Header("Race Settings")]
    public bool raceHasStarted = false;

    [Header("General UI References")]
    public Image countdownImage;
    public Sprite[] countdownSprites;
    public TextMeshProUGUI resultsText;
    public GameObject resultsPanel;

    [Header("Champion Sprite UI - Rank")]
    [Tooltip("The Image component displaying the 1st-12th place sprite.")]
    public Image rankImage;
    [Tooltip("Array of sprites from 1st to 12th. Index 0 = 1st, Index 11 = 12th.")]
    public Sprite[] rankSprites;

    [Header("Champion Sprite UI - Lap")]
    [Tooltip("The parent object holding the 'LAP X/Y' background and numbers (to hide when finished).")]
    public GameObject lapUIContainer;
    [Tooltip("The Image component displaying the current lap number (1, 2, 3).")]
    public Image currentLapImage;
    [Tooltip("The Image component displaying the TOTAL lap number (the '/Y' half).")]
    public Image totalLapImage;
    [Tooltip("Array of lap numbers. Index 0 = '1', Index 1 = '2', Index 2 = '3'.")]
    public Sprite[] lapSprites;

    [Header("Countdown Animation Settings")]
    [Tooltip("How long the sprite takes to fade and shrink in.")]
    public float animationDuration = 0.5f;
    [Tooltip("How long the sprite stays fully visible before the next one.")]
    public float holdDuration = 0.5f;
    [Tooltip("The starting size of the sprite before it shrinks to normal (1,1,1).")]
    public Vector3 startScale = new Vector3(1.5f, 1.5f, 1.5f);
    [Tooltip("How long it takes for the GO! sprite to fade out after the race starts.")]
    public float goFadeDuration = 0.5f;

    // Populated at race start from CheckpointManager's registry
    private List<RacerProgress> allRacers = new List<RacerProgress>();
    private int lastRecordedFinishCount = 0;

    // --- Leaderboard caching ---
    // Sorting every frame with LINQ allocates garbage (Enumerable iterators + new List).
    // We throttle the sort and reuse a single buffer for comparisons.
    [Header("Performance")]
    [Tooltip("How often (seconds) to re-sort the leaderboard. Champion UI is updated on this cadence.")]
    public float leaderboardRefreshInterval = 0.15f;
    private float leaderboardTimer = 0f;
    private readonly List<RacerProgress> sortedBuffer = new List<RacerProgress>();
    private RacerProgress cachedChampion;
    private int cachedChampionRank = 1;

    void Awake() { Instance = this; }

    void Start()
    {
        resultsPanel.SetActive(false);

        if (countdownSprites.Length > 0)
            StartCoroutine(StartCountdown());
        else
            BeginRace();
    }

    IEnumerator StartCountdown()
    {
        countdownImage.enabled = true;
        int lastIndex = countdownSprites.Length - 1;

        // --- 1. THE NUMBERS ---
        // Loop through everything EXCEPT the last sprite
        for (int i = 0; i < lastIndex; i++)
        {
            countdownImage.sprite = countdownSprites[i];

            float elapsedTime = 0f;
            Color imgColor = countdownImage.color;

            // Animate the pop, shrink, and fade
            while (elapsedTime < animationDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / animationDuration;
                float easedT = Mathf.SmoothStep(0f, 1f, t);

                imgColor.a = Mathf.Lerp(0f, 1f, easedT);
                countdownImage.color = imgColor;
                countdownImage.rectTransform.localScale = Vector3.Lerp(startScale, Vector3.one, easedT);

                yield return null;
            }

            imgColor.a = 1f;
            countdownImage.color = imgColor;
            countdownImage.rectTransform.localScale = Vector3.one;

            yield return new WaitForSeconds(holdDuration);
        }

        // --- 2. THE "GO!" SPRITE ---
        if (countdownSprites.Length > 0)
        {
            countdownImage.sprite = countdownSprites[lastIndex];

            // Instantly display GO! at full size and opacity
            Color finalColor = countdownImage.color;
            finalColor.a = 1f;
            countdownImage.color = finalColor;
            countdownImage.rectTransform.localScale = Vector3.one;

            // START THE RACE! (NPCs start driving immediately)
            BeginRace();

            // Optional: Hold the GO! sprite fully visible for a split second before fading
            yield return new WaitForSeconds(0.2f);

            // --- 3. FADE OUT "GO!" ---
            float fadeTime = 0f;
            while (fadeTime < goFadeDuration)
            {
                fadeTime += Time.deltaTime;
                float t = fadeTime / goFadeDuration;

                // Fade alpha from 1 down to 0
                finalColor.a = Mathf.Lerp(1f, 0f, t);
                countdownImage.color = finalColor;

                yield return null;
            }
        }

        // Clean up
        countdownImage.enabled = false;
    }

    /// <summary>
    /// Called once the countdown finishes. Grabs the racer list from
    /// CheckpointManager, sets everyone to lap 1, and flips raceHasStarted.
    /// </summary>
    void BeginRace()
    {
        raceHasStarted = true;

        // Grab all registered racers from the checkpoint system
        if (CheckpointManager.Instance != null)
        {
            allRacers = new List<RacerProgress>(CheckpointManager.Instance.allRacers);
        }

        // If CheckpointManager hasn't registered them yet (timing), find them directly
        if (allRacers.Count == 0)
        {
            allRacers.AddRange(FindObjectsByType<RacerProgress>(FindObjectsSortMode.None));
        }

        // Initialize everyone to lap 1 so they don't need to cross
        // the start/finish line before their first lap counts
        foreach (var racer in allRacers)
        {
            racer.currentLap = 1;

            // Also sync to NPCMovement for any systems reading it there
            NPCMovement movement = racer.GetComponent<NPCMovement>();
            if (movement != null)
            {
                movement.currentLap = 1;
            }
        }

        // Set the "/Y" portion of the lap UI once — totalLaps never changes
        // mid-race, so there's no need to keep re-assigning it each frame.
        int totalLaps = CheckpointManager.Instance != null
            ? CheckpointManager.Instance.totalLaps
            : 3;
        SetTotalLapSprite(totalLaps);
    }

    void Update()
    {
        if (!raceHasStarted) return;

        // Refresh racer list if it was empty at start (late spawns)
        if (allRacers.Count == 0 && CheckpointManager.Instance != null)
        {
            allRacers = new List<RacerProgress>(CheckpointManager.Instance.allRacers);
        }

        UpdateChampionUI();
        CheckForNewFinishers();
    }

    /// <summary>
    /// Updates the Champion's current rank and lap sprites.
    /// Throttles the full leaderboard sort (leaderboardRefreshInterval) to avoid
    /// allocating a fresh sorted List every frame via LINQ.
    /// </summary>
    void UpdateChampionUI()
    {
        if (allRacers.Count == 0) return;

        // Find and cache the Champion reference once (name lookup is linear)
        if (cachedChampion == null)
        {
            cachedChampion = allRacers.Find(r => r != null && r.gameObject.name == "Champion");
        }
        if (cachedChampion == null) return;

        int totalLaps = CheckpointManager.Instance != null
            ? CheckpointManager.Instance.totalLaps
            : 3;

        if (cachedChampion.isFinished)
        {
            SetRankSprite(GetFinishPlace(cachedChampion));
            if (lapUIContainer != null && lapUIContainer.activeSelf) lapUIContainer.SetActive(false);
            return;
        }

        // Re-sort only on the throttled cadence
        leaderboardTimer -= Time.deltaTime;
        if (leaderboardTimer <= 0f)
        {
            leaderboardTimer = leaderboardRefreshInterval;
            RebuildSortedBuffer();
            cachedChampionRank = sortedBuffer.IndexOf(cachedChampion) + 1;
            if (cachedChampionRank <= 0) cachedChampionRank = 1;
        }

        SetRankSprite(cachedChampionRank);
        SetLapSprite(Mathf.Min(cachedChampion.currentLap, totalLaps));

        if (lapUIContainer != null && !lapUIContainer.activeSelf)
        {
            lapUIContainer.SetActive(true);
        }
    }

    /// <summary>
    /// Sorts allRacers by race progress into a reused buffer.
    /// Uses List.Sort (in-place) instead of LINQ OrderByDescending
    /// which would allocate iterators and a new list each call.
    /// </summary>
    void RebuildSortedBuffer()
    {
        sortedBuffer.Clear();
        for (int i = 0; i < allRacers.Count; i++)
        {
            if (allRacers[i] != null) sortedBuffer.Add(allRacers[i]);
        }
        sortedBuffer.Sort(CompareByProgressDesc);
    }

    static int CompareByProgressDesc(RacerProgress a, RacerProgress b)
    {
        return b.GetRaceProgress().CompareTo(a.GetRaceProgress());
    }

    /// <summary>
    /// Updates the Image component with the correct rank sprite.
    /// </summary>
    void SetRankSprite(int rank)
    {
        if (rankImage != null && rankSprites != null && rankSprites.Length > 0)
        {
            // Subtract 1 because arrays are 0-indexed (rank 1 = index 0)
            int spriteIndex = Mathf.Clamp(rank - 1, 0, rankSprites.Length - 1);
            rankImage.sprite = rankSprites[spriteIndex];
        }
    }

    /// <summary>
    /// Updates the Image component with the correct lap number sprite.
    /// </summary>
    void SetLapSprite(int lap)
    {
        if (currentLapImage != null && lapSprites != null && lapSprites.Length > 0)
        {
            // Subtract 1 because arrays are 0-indexed (lap 1 = index 0)
            int spriteIndex = Mathf.Clamp(lap - 1, 0, lapSprites.Length - 1);
            currentLapImage.sprite = lapSprites[spriteIndex];
        }
    }

    /// <summary>
    /// Fills in the "/Y" half of the lap UI with the total-lap sprite.
    /// Called once when the race begins since total laps is fixed for the race.
    /// </summary>
    void SetTotalLapSprite(int totalLaps)
    {
        if (totalLapImage != null && lapSprites != null && lapSprites.Length > 0)
        {
            int spriteIndex = Mathf.Clamp(totalLaps - 1, 0, lapSprites.Length - 1);
            totalLapImage.sprite = lapSprites[spriteIndex];
            totalLapImage.enabled = true;
        }
    }

    /// <summary>
    /// Polls CheckpointManager.finishOrder for new finishers and
    /// updates the results panel with their colored names.
    /// </summary>
    void CheckForNewFinishers()
    {
        if (CheckpointManager.Instance == null) return;

        var finishOrder = CheckpointManager.Instance.finishOrder;

        // Only update if new finishers have been recorded
        if (finishOrder.Count <= lastRecordedFinishCount) return;

        resultsPanel.SetActive(true);

        string results = "<b>Race Results:</b>\n";

        for (int i = 0; i < finishOrder.Count; i++)
        {
            RacerProgress racer = finishOrder[i];
            string racerName = racer.gameObject.name;
            Color racerColor = GetRacerColor(racer.gameObject);
            string hexColor = ColorUtility.ToHtmlStringRGB(racerColor);
            string suffix = GetOrdinalSuffix(i + 1);

            results += $"{i + 1}{suffix}: <color=#{hexColor}>{racerName}</color>\n";
        }

        resultsText.text = results;
        lastRecordedFinishCount = finishOrder.Count;
    }

    /// <summary>
    /// Gets the finishing position of a racer from CheckpointManager's finish order.
    /// </summary>
    int GetFinishPlace(RacerProgress racer)
    {
        if (CheckpointManager.Instance == null) return 0;

        int index = CheckpointManager.Instance.finishOrder.IndexOf(racer);
        return index >= 0 ? index + 1 : 0;
    }

    /// <summary>
    /// Reads the racer's color from RacerColorAssigner if present,
    /// otherwise falls back to the first MeshRenderer material.
    /// </summary>
    Color GetRacerColor(GameObject racer)
    {
        RacerColorAssigner colorAssigner = racer.GetComponent<RacerColorAssigner>();
        if (colorAssigner != null)
        {
            return colorAssigner.GetRacerColor();
        }

        MeshRenderer renderer = racer.GetComponentInChildren<MeshRenderer>();
        if (renderer != null && renderer.material != null)
        {
            return renderer.material.color;
        }
        return Color.white;
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