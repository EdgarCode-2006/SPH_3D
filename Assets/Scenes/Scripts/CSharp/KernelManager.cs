using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using SPH.Common;

class kernelManager
{
    ComputeBuffers computeBuffers;
    Kernels kernels;
    private ParticleSettings settings;

    // Reused every frame instead of allocating a new one per frame (avoids GC churn).
    private MaterialPropertyBlock renderProps = new MaterialPropertyBlock();

    public kernelManager(ParticleSettings inputSettings)
    {
        settings = inputSettings;

        // Grid resolution is rounded up per axis so it matches exactly what the shaders
        // index with (PositionToIndex uses resolution.x/y/z as integers). The grid covers
        // the box plus the wall particle layers outside it.
        float cellsPerAxis = Mathf.Ceil(BoundGeneration.gridHalfWidth(settings) * 2 / settings.smoothingRadius);
        settings.resolution = new Vector3(cellsPerAxis, cellsPerAxis, cellsPerAxis);
        settings.cellCount = (int)cellsPerAxis * (int)cellsPerAxis * (int)cellsPerAxis;
        settings.blockSize = 256;

        // padding cellCounts to the next multiple of 256
        int remainder = settings.cellCount % settings.blockSize;
        if (remainder != 0)
            settings.cellCount += settings.blockSize - remainder;
    }

    // Puts every particle back at its starting position. Refills the existing buffers, so
    // the buffers bound to the kernels and the material stay valid.
    public void initParticles()
    {
        InitBuffers.resetParticles(settings, computeBuffers);
    }

    /*
    * Checks that the starting shape fits inside the simulation box. If it doesn't,
    * particles start outside the grid, get clamped into a handful of edge cells, and
    * the neighbour search in CSMain becomes so expensive that Windows resets the GPU
    * (TDR), which kills the Unity editor. Better to refuse to start with a clear message.
    */
    public bool validateSettings()
    {
        // The first wall layer sits on the box plane, so the fluid has to start at least one
        // wall spacing inside it (otherwise it spawns on top of wall particles).
        float clearance = Mathf.Max(settings.particleRadius, BoundGeneration.WALL_SPACING);
        float needed = InitBuffers.getSpawnHalfExtent(settings) + clearance;

        if (needed > settings.boxWidth)
        {
            Debug.LogError(
                $"[SPH] {settings.particleCount} particles in a {settings.generatedShape} shape need " +
                $"Box Width >= {Mathf.Ceil(needed)} (currently {settings.boxWidth}). Simulation not started.");
            return false;
        }
        return true;
    }

    public void init()
    {
        InitBuffers.InitAllBuffers(ref computeBuffers, settings);
        InitKernels.initKernelData(settings, ref computeBuffers, ref kernels);
    }

    /*
    * this is the kernel used in order to intialize a new fresh compute buffer
    * during the blelloch scan of the sums generated. it is created so that we
    * can accomodate for new sums if the element count is greater than 256.
    * the size of our zero256k array is only 256+2 because we wont ever go past
    * the 256 + 1 mark since the kernels get divided into 2 seperate kernels when that
    * happens and get recursively merged back into a single one.
    */
    static uint[] zero256k = new uint[256+2];
    static public ComputeBuffer initNextSums(int size)
    {
        var buffer = new ComputeBuffer(size, sizeof(uint));

        // Only copy the portion you need; the rest is zero
        buffer.SetData(zero256k, 0, 0, size);
        return buffer;
    }

    private void dispatchKernels()
    {
        // Re-upload the inspector-tweakable values so changes in Play mode take effect.
        InitKernels.setTunableParameters(settings);

        // Defines the thread group needed for running these kernels on the GPU.
        // Walls are included so they get hashed into the grid like every other particle.
        int threadGroupCount = Mathf.CeilToInt((float)settings.totalParticleCount / settings.blockSize);
        threadGroupCount = Mathf.Max(1, threadGroupCount);

        // clear the cellCounts for each cell as the particles moved since the prev frame
        RuntimeOperations.clearBuffer(computeBuffers.cellCounts, settings.cellCount, settings, kernels);

        // Dispatching the particle counter for each cell.
        settings.spacialIndexerComputeShader.Dispatch(kernels.PerCellCounter, threadGroupCount, 1, 1);

        DispatchBlelloch.blellochScanRecursive(settings, kernels, settings.cellCount,
            computeBuffers.cellCounts, computeBuffers.blellochScanned);

        settings.spacialIndexerComputeShader.SetBuffer(kernels.fillParticleIndexList, "inCellOffset", computeBuffers.cellOffsets);
        settings.spacialIndexerComputeShader.SetBuffer(kernels.fillParticleIndexList, "cellStarts", computeBuffers.blellochScanned);

        settings.spacialIndexerComputeShader.Dispatch(kernels.fillParticleIndexList, threadGroupCount, 1, 1);

        RuntimeOperations.clearBuffer(computeBuffers.cellOffsets, settings.cellCount, settings, kernels);

        // Copy positions / velocities into grid-cell order (the sorted buffers).
        settings.spacialIndexerComputeShader.Dispatch(kernels.reorderParticles, threadGroupCount, 1, 1);

        // Density + pressure of every particle, then forces + integration.
        DispatchMain.dispatchDensityKernel(settings, kernels.densityKernel);
        DispatchMain.dispatchMainKernel(settings, kernels.mainKernel);
    }

    /*
    * This function is ran every frame. it dispatches the kernels and execute the drawMesh
    * function in order to render each particle using the GPU.
    */
    public void displayParticles(bool simulate)
    {
        if (simulate)
            dispatchKernels();

        RenderParams rp = new RenderParams(settings.particleMaterial);

        // Bounds must enclose the simulation box, otherwise Unity culls every instance
        // as soon as the box is moved away from the origin.
        float extent = (BoundGeneration.gridHalfWidth(settings) + settings.particleRadius) * 2f;
        rp.worldBounds = new Bounds(settings.particleBoundCenter, Vector3.one * extent);

        // Attach buffer to the material. Uniforms declared in ParticleData.hlsl are NOT
        // shared with materials: anything the render shader reads must be set here.
        // positions (w = wall flag) and density come from the same slot order:
        // CSMain writes positions[slot], DensityPass writes sortedDensityPressure[slot].
        renderProps.SetBuffer("positions", computeBuffers.positions);
        renderProps.SetBuffer("sortedDensityPressure", computeBuffers.sortedDensityPressure);
        renderProps.SetFloat("particleRadius", settings.particleRadius);
        renderProps.SetFloat("restDensity", settings.restDensity);
        rp.matProps = renderProps;

        // Every particle is drawn, fluid and walls mixed together: the buffers are in
        // grid-cell order, so the walls are no longer a block at the end. When the
        // material's "Wall Particle Alpha" is 0, the vertex shader collapses wall squares
        // to nothing (6 vertices each, no pixels).
        int instanceCount = settings.totalParticleCount;

        // One camera-facing square per particle: 6 vertices (2 triangles) per instance,
        // built in ParticleShader from SV_VertexID. No mesh is needed (settings.mesh is unused).
        Graphics.RenderPrimitives(
            rp,
            MeshTopology.Triangles,
            6,
            instanceCount
        );

        // DebugHelper.printParticles(settings, computeBuffers.particleBuffer);
        // DebugHelper.printCellCounts(settings, computeBuffers.cellCounts);
        // DebugHelper.printBlellochScanned(settings, computeBuffers.blellochScanned);
        // DebugHelper.printDebugBuffer(computeBuffers.debugBuffer, settings, 1);
    }

    public void Dispose()
    {
        CleanUp.Dispose(ref computeBuffers);
    }
}