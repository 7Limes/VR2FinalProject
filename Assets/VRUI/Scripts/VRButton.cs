using UnityEngine;
using UnityEngine.Events;

public class VRButton : MonoBehaviour
{
    [SerializeField] private GameObject buttonVisual;

    [SerializeField] private Transform pressTransform;
    [SerializeField] private float pressDuration = 0.2f;

    [SerializeField] private AudioClip pressSound = null;
    [SerializeField] private AudioClip releaseSound = null;

    [SerializeField] private UnityEvent pressEvent;

    
    enum State
    {
        Idle,
        Pressing,
        Releasing
    }

    private AudioSource audioSource;

    private State state = State.Idle;
    private Vector3 initialPosition, pressPosition;
    private float pressTimer = 0f;
    
    public void SelectEnter()
    {
        pressEvent.Invoke();

        if (pressSound != null)
            audioSource.PlayOneShot(pressSound);
        
        state = State.Pressing;
        pressTimer = 0f;
    }

    public void SelectExit()
    {
        pressEvent.Invoke();

        if (releaseSound != null)
            audioSource.PlayOneShot(releaseSound);

        state = State.Releasing;
        pressTimer = 0f;
    }

    private void Start()
    {
        initialPosition = buttonVisual.transform.position;
        pressPosition = pressTransform.position;

        audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (state != State.Idle)
        {
            pressTimer = Mathf.MoveTowards(pressTimer, pressDuration, Time.deltaTime);
            float t = pressTimer / pressDuration;
            Vector3 newPosition = Vector3.zero;

            switch (state)
            {
                case State.Pressing:
                    newPosition = Vector3.Lerp(initialPosition, pressPosition, t);
                    break;
                case State.Releasing:
                    newPosition = Vector3.Lerp(pressPosition, initialPosition, t);
                    break;
            }

            buttonVisual.transform.position = newPosition;

            if (pressTimer == pressDuration)
            {
                state = State.Idle;
            }
        }
    }
}
