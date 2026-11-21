using UnityEngine;

namespace Common
{
    public enum GenerationType { Cube, Sphere, Torus }

}

[CreateAssetMenu(fileName = "ParticleSettings", menuName = "Particles/Settings")]

public class ParticleSettings : ScriptableObject
{
    public int particleCount = 256;
    public float particleRadius = 2f;
    public float gravityForce = 9.81f;
    public float boxWidth = 10f;
    public float smoothingRadius = 1.2f;
    public float timeStep = 0.4f;
    public float restDensity = 0.2f;
    public float viscosityConstant = 0.64f;
    public float collisionDampening = 0.5f;
    public float dampening = 50f;
    public float particleMass = 0.5f;
    public Material particleMaterial;
    public Vector3 particleBoundCenter = Vector3.zero;
    public Mesh mesh;
    public Material debugWireFrame;
    public ComputeShader mainComputeShader;
    public ComputeShader spacialIndexerComputeShader;
    public ComputeShader BlellochScanComputeShader;

    // Derived at runtime by kernelManager. NonSerialized so they are never written
    // back into the .asset file (previously stale values were being saved to disk).
    [System.NonSerialized]
    public int cellCount = 0;
    [System.NonSerialized]
    public int blockSize = 256;
    [System.NonSerialized]
    public Vector3 resolution = Vector3.zero;

    // Number of fixed wall particles, set when the particle buffer is built. They are
    // stored after the fluid particles: fluid = [0, particleCount),
    // walls = [particleCount, totalParticleCount).
    [System.NonSerialized]
    public int boundaryParticleCount = 0;
    public int totalParticleCount => particleCount + boundaryParticleCount;
    public Common.GenerationType generatedShape;
}
