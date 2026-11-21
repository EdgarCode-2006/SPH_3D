using UnityEngine;

public class simMain : MonoBehaviour
{
    public ParticleSettings particleSettings;
    private bool simulationStarted = false;
    private kernelManager kernelManager;

    void Start()
    {
        kernelManager = new kernelManager(particleSettings);

        // Don't allocate anything or dispatch if the setup would overload the GPU.
        // Disabling the component stops Update(); OnDestroy still runs (Dispose is null-safe).
        if (!kernelManager.validateSettings())
        {
            enabled = false;
            return;
        }

        kernelManager.init();

        DebugHelper.displayWireFrame(particleSettings.boxWidth,
                                    particleSettings.particleBoundCenter,
                                    particleSettings.debugWireFrame);
    }

    void Update()
    {
        if (Input.GetKeyDown("space"))
        {
            simulationStarted = true;
        }

        if (simulationStarted == true)
        {
            kernelManager.displayParticles(simulate: true);
        } else
        {
            kernelManager.displayParticles(simulate: false);
            // kernelManager.initParticles();
        }
    }
    
    void OnDestroy()
    {
        kernelManager?.Dispose();
    }
}
