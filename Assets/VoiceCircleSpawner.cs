using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class VoiceCircleSpawner : NetworkBehaviour
{
    [Header("Visuals")]
    [Tooltip("The circle prefab to spawn and scale.")]
    public GameObject circlePrefab;
    
    [Tooltip("Multiplier for how big the circle gets based on loudness.")]
    public float scaleSensitivity = 5f; 
    
    [Tooltip("Minimum dB required to show the circle. (Digital audio is usually -80 to 0)")]
    public float minDbThreshold = -50f;

    [Header("Animation Speeds")]
    [Tooltip("How fast the circle expands when you start talking.")]
    public float expandSpeed = 15f; 
    
    [Tooltip("How fast the circle shrinks when you stop. Lower = lingers longer.")]
    public float shrinkSpeed = 3f;

    // Internal Mic Data
    private AudioClip micClip;
    private string micDevice;
    private const int SAMPLE_RATE = 44100;
    private const int SAMPLE_WINDOW = 256;

    // Sync the loudness float to all clients automatically
    private NetworkVariable<float> networkLoudnessDb = new NetworkVariable<float>(
        -80f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private GameObject currentCircle;

    public override void OnNetworkSpawn()
    {
        // Keep all OG comments: Only the owner should actively record their own microphone
        if (IsOwner)
        {
            if (Microphone.devices.Length > 0)
            {
                micDevice = Microphone.devices[0]; // Grab the default system mic
                
                // Start a looping 1-second recording buffer
                micClip = Microphone.Start(micDevice, true, 1, SAMPLE_RATE);
            }
            else
            {
                Debug.LogWarning("No microphone device detected on this client!");
            }
        }

        // Spawn a local visual representation for this player on all clients
        if (circlePrefab != null)
        {
            currentCircle = Instantiate(circlePrefab, transform.position, Quaternion.identity);
            currentCircle.transform.SetParent(transform); // Attach it to the player object
            currentCircle.transform.localScale = Vector3.zero; // Start invisible
        }
    }

    void Update()
    {
        // Only the owner calculates mic input and updates the network variable
        if (IsOwner && micClip != null)
        {
            networkLoudnessDb.Value = GetLoudnessInDecibels();
        }

        // All clients (including the owner) update the visual based on the networked value
        if (currentCircle != null)
        {
            UpdateCircleVisual(networkLoudnessDb.Value);
        }
    }

    private float GetLoudnessInDecibels()
    {
        // Get the current read position of the microphone buffer
        int micPosition = Microphone.GetPosition(micDevice) - SAMPLE_WINDOW + 1;
        
        // Return silence if the mic hasn't recorded enough samples yet
        if (micPosition < 0) return -80f; 

        float[] waveData = new float[SAMPLE_WINDOW];
        micClip.GetData(waveData, micPosition);

        // 1. Calculate Root Mean Square (RMS)
        float totalSquare = 0f;
        for (int i = 0; i < SAMPLE_WINDOW; i++)
        {
            totalSquare += waveData[i] * waveData[i];
        }
        float rms = Mathf.Sqrt(totalSquare / SAMPLE_WINDOW);

        // 2. Convert RMS to Decibels (dB)
        // Unity audio is normalized between -1 and 1. 0 RMS means -Infinity dB.
        float db = 20f * Mathf.Log10(rms);

        // Clamp to avoid negative infinity errors during complete silence
        if (float.IsInfinity(db) || float.IsNaN(db))
        {
            db = -80f;
        }

        return db;
    }

    private void UpdateCircleVisual(float currentDb)
    {
        float targetScaleValue = 0f;

        // Hide the circle if it's just quiet background noise
        if (currentDb >= minDbThreshold)
        {
            // Map the decibel range (e.g., -80 to 0) to a positive normalized value (0 to 1)
            float normalizedLoudness = Mathf.InverseLerp(-80f, 0f, currentDb);
            
            // Apply sensitivity multiplier
            targetScaleValue = normalizedLoudness * scaleSensitivity;
        }

        Vector3 newScale = new Vector3(targetScaleValue, targetScaleValue, targetScaleValue);

        // Determine if the circle is trying to get bigger or smaller right now
        bool isExpanding = targetScaleValue > currentCircle.transform.localScale.x;
        
        // Pick the fast speed for growing, or the slow speed for shrinking
        float currentSpeed = isExpanding ? expandSpeed : shrinkSpeed;

        // Smoothly interpolate the scale to prevent jittery visuals
        currentCircle.transform.localScale = Vector3.Lerp(
            currentCircle.transform.localScale, 
            newScale, 
            Time.deltaTime * currentSpeed
        );
    }

    public override void OnNetworkDespawn()
    {
        // Keep all OG comments: Ensure we release the microphone hardware when the player disconnects/despawns
        if (IsOwner && micDevice != null)
        {
            Microphone.End(micDevice);
        }
    }
}