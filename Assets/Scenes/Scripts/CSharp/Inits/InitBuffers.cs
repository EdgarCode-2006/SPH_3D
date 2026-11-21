using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using SPH.Common;
using System;
using System.Collections.Generic;

/*
* This class is used in order to initialize the buffer data for the GPU to
* work on. It is static and here for seperation of concerns and organization
* of code as having all of this buffer initialization in kernel manager is
* too verbose.
*/
public static class InitBuffers
{
    static private Vector4 getSphericalPosition(int currentIndex, int dimensions, ParticleSettings settings)
    {
        float u = (currentIndex + 0.5f) / settings.particleCount;
        float v = Mathf.Repeat(currentIndex * 0.618033988f, 1f);
        float theta = 2f * Mathf.PI * v;
        float phi = Mathf.Acos(1f - (2f * u));

        Vector3 p = new Vector3(
            dimensions * Mathf.Sin(phi) * Mathf.Cos(theta),
            dimensions * Mathf.Cos(phi),
            dimensions * Mathf.Sin(phi) * Mathf.Sin(theta)
        );

        return p + settings.particleBoundCenter;
    }

    static private Vector4 getTorusPosition(int currentIndex, int dimensions, ParticleSettings settings)
    {
        float R = dimensions;
        float r = dimensions / 2;

        float u = (currentIndex + 0.5f) / settings.particleCount;
        float v = Mathf.Repeat(currentIndex * 0.618033988f, 1f);
        float theta = 2f * Mathf.PI * v;
        float phi = 2f * Mathf.PI * u;

        Vector3 p = new Vector3(
            (R + (r * Mathf.Cos(phi))) * Mathf.Cos(theta),
            r * Mathf.Sin(phi),
            (R + (r * Mathf.Cos(phi))) * Mathf.Sin(theta)
        );

        return p + settings.particleBoundCenter;
    }
    
    // Distance between neighbouring particles in the Cube spawn, and the random offset
    // added to each one so the lattice isn't perfectly regular.
    // Public because the wall particles (BoundGeneration) use the same spacing.
    public const float CUBE_SPACING = 1f;
    const float CUBE_JITTER = 0.1f;

    // Every other horizontal (y) layer is shifted in x and z so a particle sits over the
    // gap between four particles below instead of directly on top of one. Layers are
    // moved by -/+ this many spacings, so neighbouring layers are offset by 2x this
    // (half a spacing) and the block stays centred.
    const float CUBE_STAGGER = 0.25f;

    // Particles per side used by every generator: the cube root of the particle count.
    static public int spawnDimensions(ParticleSettings settings)
    {
        return Mathf.Max(1, Mathf.RoundToInt(Mathf.Pow(settings.particleCount, 1f / 3f)));
    }

    // Number of z layers the Cube needs. Equals dimensions when particleCount is a perfect
    // cube; otherwise there is a partial extra layer (e.g. 100000 -> 46 x 46 x 48).
    static private int cubeLayers(int dimensions, ParticleSettings settings)
    {
        return Mathf.CeilToInt((float)settings.particleCount / (dimensions * dimensions));
    }

    /*
    * Largest distance (on any axis) from particleBoundCenter to a spawned particle's
    * centre. Used to check that the starting shape fits inside the simulation box.
    */
    static public float getSpawnHalfExtent(ParticleSettings settings)
    {
        int dimensions = spawnDimensions(settings);

        switch (settings.generatedShape)
        {
            case Common.GenerationType.Cube:
                int longestSide = Mathf.Max(dimensions, cubeLayers(dimensions, settings));
                return ((longestSide - 1) * 0.5f + CUBE_STAGGER) * CUBE_SPACING + CUBE_JITTER;
            case Common.GenerationType.Sphere:
                return dimensions;                 // shell of radius = dimensions
            case Common.GenerationType.Torus:
                return dimensions * 1.5f;          // R + r, with r = R / 2
            default:
                return 0f;
        }
    }

    // The function getPosition takes an index in a 1D array of particles and computes
    // its position in a 3D grid in order to display the particles in a cube shape at the start
    // of the simulation while keeping the memory linear in a 1D array that we can
    // easily send to the GPU. The cube is centred on particleBoundCenter.
    static private Vector4 getCubicPosition(int currentIndex, int dimensions, ParticleSettings settings)
    {
        int xIndex = currentIndex % dimensions;
        int yIndex = (currentIndex / dimensions) % dimensions;
        int zIndex = currentIndex / (dimensions * dimensions);

        // offsets that move index 0..n-1 to -(n-1)/2..+(n-1)/2 so the block is centred.
        float halfXY = (dimensions - 1) * 0.5f;
        float halfZ = (cubeLayers(dimensions, settings) - 1) * 0.5f;

        // staggered stacking: even layers shift -CUBE_STAGGER, odd layers +CUBE_STAGGER.
        float stagger = (yIndex % 2 == 0) ? -CUBE_STAGGER : CUBE_STAGGER;

        Vector3 local = new Vector3(
            xIndex - halfXY + stagger,
            yIndex - halfXY,
            zIndex - halfZ + stagger) * CUBE_SPACING;
        Vector3 jitter = new Vector3(
            UnityEngine.Random.Range(-CUBE_JITTER, CUBE_JITTER),
            UnityEngine.Random.Range(-CUBE_JITTER, CUBE_JITTER),
            UnityEngine.Random.Range(-CUBE_JITTER, CUBE_JITTER));

        Vector3 p = settings.particleBoundCenter + local + jitter;
        return new Vector4(p.x, p.y, p.z, 0);
    }

    public static Dictionary<Common.GenerationType, Func<int, int, ParticleSettings, Vector4>> Generators
        = new()
    {
        { Common.GenerationType.Cube,   getCubicPosition },
        { Common.GenerationType.Sphere, getSphericalPosition },
        { Common.GenerationType.Torus,  getTorusPosition }
    };

    /*
    * Builds every particle of the simulation in one array: the fluid first, then the fixed
    * wall particles from BoundGeneration.
    *   fluid: [0, particleCount)                    isBoundary = 0
    *   walls: [particleCount, totalParticleCount)   isBoundary = 1
    * Also sets settings.boundaryParticleCount, which every buffer size and dispatch uses.
    *
    * Walls never move (velocity stays 0, so in the viscosity sum they act as a still
    * surface), but DensityPass recomputes their density and pressure every step, so they
    * push back harder the more the fluid is compressed against them.
    * This order only holds at the start: after the first step the GPU buffers are kept in
    * grid-cell order, and walls are recognised by position.w = 1.
    */
    static private Particle[] buildParticles(ParticleSettings settings)
    {
        Vector2 boundX = new Vector2(settings.particleBoundCenter.x - settings.boxWidth, settings.particleBoundCenter.x + settings.boxWidth);
        Vector2 boundY = new Vector2(settings.particleBoundCenter.y - settings.boxWidth, settings.particleBoundCenter.y + settings.boxWidth);
        Vector2 boundZ = new Vector2(settings.particleBoundCenter.z - settings.boxWidth, settings.particleBoundCenter.z + settings.boxWidth);

        List<Vector4> wallPositions = BoundGeneration.GenerateParticleBound(boundX, boundY, boundZ, settings);
        settings.boundaryParticleCount = wallPositions.Count;

        Particle[] particles = new Particle[settings.totalParticleCount];
        int dimensions = spawnDimensions(settings);

        for (int i = 0; i < settings.particleCount; i++)
        {
            particles[i] = new Particle
            {
                position = Generators[settings.generatedShape](i, dimensions, settings),
                velocity = Vector4.zero,
                buffer2 = Vector4.zero,
                density = settings.restDensity,
                pressure = 0.0f,
                isBoundary = 0,
                mortonCode = 0
            };
        }

        for (int w = 0; w < wallPositions.Count; w++)
        {
            particles[settings.particleCount + w] = new Particle
            {
                position = wallPositions[w],
                velocity = Vector4.zero,
                buffer2 = Vector4.zero,
                density = settings.restDensity,
                pressure = 0.0f,
                isBoundary = 1,
                mortonCode = 0
            };
        }
        return particles;
    }

    /*
    * Splits the particles into the per-field GPU buffers:
    *   positions             xyz = position, w = isBoundary (1 = wall, 0 = fluid)
    *   velocities            xyz = velocity
    *   sortedDensityPressure x = density, y = pressure. Normally written by DensityPass;
    *                         filled here only so the first frames (before Space is pressed)
    *                         render with the rest density colour.
    */
    static private void uploadParticles(Particle[] particles, ComputeBuffers buffers)
    {
        int count = particles.Length;
        Vector4[] positions = new Vector4[count];
        Vector4[] velocities = new Vector4[count];
        Vector2[] densityPressure = new Vector2[count];

        for (int i = 0; i < count; i++)
        {
            Vector4 p = particles[i].position;
            positions[i] = new Vector4(p.x, p.y, p.z, particles[i].isBoundary);
            velocities[i] = particles[i].velocity;
            densityPressure[i] = new Vector2(particles[i].density, particles[i].pressure);
        }

        buffers.positions.SetData(positions);
        buffers.velocities.SetData(velocities);
        buffers.sortedDensityPressure.SetData(densityPressure);
    }

    // Resets the particles (fluid + walls) in the existing buffers to their starting state.
    static public void resetParticles(ParticleSettings settings, ComputeBuffers buffers)
    {
        uploadParticles(buildParticles(settings), buffers);
    }

    /*
    * Creates the particle data buffers (state + sorted copies) and uploads the starting
    * particles. The sorted position/velocity buffers start empty: ReorderParticles fills
    * them at the beginning of every step.
    */
    static public void initParticleBuffers(ref ComputeBuffers buffers, ParticleSettings settings)
    {
        // Built first so the buffers are sized for fluid + walls.
        Particle[] particles = buildParticles(settings);
        int count = particles.Length;

        buffers.positions = new ComputeBuffer(count, sizeof(float) * 4);
        buffers.velocities = new ComputeBuffer(count, sizeof(float) * 4);
        buffers.sortedPositions = new ComputeBuffer(count, sizeof(float) * 4);
        buffers.sortedVelocities = new ComputeBuffer(count, sizeof(float) * 4);
        buffers.sortedDensityPressure = new ComputeBuffer(count, sizeof(float) * 2);

        uploadParticles(particles, buffers);
    }

    /*
    * This function is used in order to initialiwe the debug buffer which allows us
    * to set data during compute shader execution and read them back later inside of
    * the debugHelper structure for creating print statement for GPU.
    */
    static public void initDebugBuffer(ref ComputeBuffer debugBuffer, ParticleSettings settings)
    {
        int stride = UnsafeUtility.SizeOf<DebugStruct>();

        debugBuffer = new ComputeBuffer(settings.totalParticleCount, stride);
        DebugStruct[] debugData = new DebugStruct[settings.totalParticleCount];

        for (uint i = 0; i < settings.totalParticleCount; i++)
        {
            debugData[i] = new DebugStruct
            {
                float4Value1 = Vector4.zero,
                floatValue1 = -1,
                floatValue2 = -1,
                uintValue1 = 0,
                uintValue2 = 0
            };
        }
        debugBuffer.SetData(debugData);
    }

    /*
    * This function is used in order to initialize all the unsigned int buffers to a given value
    * at the start of the program.
    */
    static public void initUintBuffer(ref ComputeBuffer buffer, int size, uint value = 0)
    {
        int stride = sizeof(uint);
        buffer = new ComputeBuffer(size, stride, ComputeBufferType.Structured);

        uint[] data = new uint[size];
        for (int i = 0; i < size; i++) data[i] = value;

        buffer.SetData(data);
    }

    /*
    * here we initialize all the buffers with a given size and parameters
    * depending on the buffer type.
    */
    static public void InitAllBuffers(
        ref ComputeBuffers computeBuffers,
        ParticleSettings settings)
    {
        // the particle data for each particle in the simulation (fluid + walls).
        // Must run first: it sets settings.boundaryParticleCount used by the sizes below.
        initParticleBuffers(ref computeBuffers, settings);

        // Related to spacial partitioning.
        initUintBuffer(ref computeBuffers.cellCounts, settings.cellCount);
        initUintBuffer(ref computeBuffers.cellOffsets, settings.cellCount);
        initUintBuffer(ref computeBuffers.blellochScanned, settings.cellCount + 1);
        initUintBuffer(ref computeBuffers.scannedSums, Mathf.CeilToInt((float)settings.cellCount / settings.blockSize) + 1);
        initUintBuffer(ref computeBuffers.particleIndexes, settings.totalParticleCount);

        // for debug.
        // initDebugBuffer(ref computeBuffers.debugBuffer, settings);
    }
}