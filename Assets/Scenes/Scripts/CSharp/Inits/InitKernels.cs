using SPH.Common;
using UnityEngine;

/*
* THis is a helper static class for the Kernel manager much like InitBUffer,
* which works towards initializing the kernels with the necessary data to
* be able to modify particle data and optimize calculation especially in
* neighbor search. This class is used after Init buffer as the buffers need
* be set in order to be used by the kernels.
*/
static public class InitKernels
{   
    /*
    * The two kernels of ComputeMain.compute. Both read the sorted (cell-order) buffers;
    * DensityPass writes sortedDensityPressure, CSMain writes positions / velocities.
    */
    static public void initMainKernel(
        ParticleSettings settings,
        ref int densityKernel,
        ref int mainKernel,
        ComputeBuffers computeBuffers)
    {
        ComputeShader shader = settings.mainComputeShader;

        densityKernel = shader.FindKernel("DensityPass");
        shader.SetBuffer(densityKernel, "sortedPositions", computeBuffers.sortedPositions);
        shader.SetBuffer(densityKernel, "sortedDensityPressure", computeBuffers.sortedDensityPressure);
        shader.SetBuffer(densityKernel, "cellStarts", computeBuffers.blellochScanned);
        shader.SetBuffer(densityKernel, "cellSizes", computeBuffers.cellCounts);

        mainKernel = shader.FindKernel("CSMain");
        shader.SetBuffer(mainKernel, "sortedPositions", computeBuffers.sortedPositions);
        shader.SetBuffer(mainKernel, "sortedVelocities", computeBuffers.sortedVelocities);
        shader.SetBuffer(mainKernel, "sortedDensityPressure", computeBuffers.sortedDensityPressure);
        shader.SetBuffer(mainKernel, "positions", computeBuffers.positions);
        shader.SetBuffer(mainKernel, "velocities", computeBuffers.velocities);
        shader.SetBuffer(mainKernel, "cellStarts", computeBuffers.blellochScanned);
        shader.SetBuffer(mainKernel, "cellSizes", computeBuffers.cellCounts);
    }
    static private void setBounds(ParticleSettings settings, ComputeShader computeShader)
    {
        Vector2 boundX = new Vector2(settings.particleBoundCenter.x - settings.boxWidth, settings.particleBoundCenter.x + settings.boxWidth);
        Vector2 boundY = new Vector2(settings.particleBoundCenter.y - settings.boxWidth, settings.particleBoundCenter.y + settings.boxWidth);
        Vector2 boundZ = new Vector2(settings.particleBoundCenter.z - settings.boxWidth, settings.particleBoundCenter.z + settings.boxWidth);

        computeShader.SetFloats("boundX", boundX.x, boundX.y);
        computeShader.SetFloats("boundY", boundY.x, boundY.y);
        computeShader.SetFloats("boundZ", boundZ.x, boundZ.y);
    }

    static private void setCommonData(ParticleSettings settings, ComputeShader computeShader)
    {
        // Grid covers the box plus the wall layers around it (see BoundGeneration.gridHalfWidth).
        Vector3 gridOrigin = settings.particleBoundCenter - Vector3.one * BoundGeneration.gridHalfWidth(settings);
        computeShader.SetVector("gridOrigin", gridOrigin);
        computeShader.SetInts("resolution",
            Mathf.CeilToInt(settings.resolution.x), Mathf.CeilToInt(settings.resolution.y),  Mathf.CeilToInt(settings.resolution.z));
        // fluid + walls: the walls have to be hashed into the grid and found as neighbours.
        computeShader.SetInt("particleCount", settings.totalParticleCount);
        computeShader.SetFloat("smoothingRadius", settings.smoothingRadius);
    }

    static private void setPreCalculatedValues(ParticleSettings settings)
    {
        float smoothingStretch = 315/(64 * Mathf.PI * Mathf.Pow(Mathf.Abs(settings.smoothingRadius), 9));
        float smoothingScalar = 45/(Mathf.PI * Mathf.Pow(settings.smoothingRadius, 6));
        float spikyKernelScalar = -45/(Mathf.PI * Mathf.Pow(settings.smoothingRadius, 6));

        settings.mainComputeShader.SetFloat("smoothingStretch", smoothingStretch);
        settings.mainComputeShader.SetFloat("smoothingScalar", smoothingScalar);
        settings.mainComputeShader.SetFloat("spikyKernelScalar", spikyKernelScalar);
    }

    /*
    * These are the physics parameters that are safe to change while the simulation runs
    * (they don't affect any buffer size or the grid layout). This is called every frame
    * so tweaking them in the inspector during Play mode takes effect immediately.
    *
    * NOT included on purpose: particleCount, boxWidth, smoothingRadius, particleBoundCenter.
    * Those define the buffer sizes / grid resolution and require a restart to change.
    */
    static public void setTunableParameters(ParticleSettings settings)
    {
        settings.mainComputeShader.SetFloat("particleRadius", settings.particleRadius);
        settings.mainComputeShader.SetFloat("gravityForce", settings.gravityForce);
        settings.mainComputeShader.SetFloat("dampening", settings.dampening);
        settings.mainComputeShader.SetFloat("collisionDampening", settings.collisionDampening);
        settings.mainComputeShader.SetFloat("restDensity", settings.restDensity);
        settings.mainComputeShader.SetFloat("timeStep", settings.timeStep);
        settings.mainComputeShader.SetFloat("viscosityConstant", settings.viscosityConstant);
        settings.mainComputeShader.SetFloat("particleMass", settings.particleMass);
    }

    /*
    * this is the data needed by the main compute shader for running the simulation with all the
    * parameters which the user can select.
    */
    static private void setMainKernelData(ParticleSettings settings)
    {
        setTunableParameters(settings);
        settings.mainComputeShader.SetInt("cellCount", settings.cellCount);
        setPreCalculatedValues(settings);

        setBounds(settings, settings.mainComputeShader);
        setBounds(settings, settings.spacialIndexerComputeShader);
        setCommonData(settings, settings.mainComputeShader);
        setCommonData(settings, settings.spacialIndexerComputeShader);
    }

    /*
    * We set the debug buffer in every kernel since we want to be able to log back data in order to debug any kernel
    * This kernel can be filled with uints floats and a float4 in order to get a log on any kind of data we might
    * want to see evolve inside of the compute shader.
    */
    static public void setDebugBuffer(ParticleSettings settings, Kernels kernels, ComputeBuffers computeBuffers)
    {
        settings.mainComputeShader.SetBuffer(kernels.mainKernel, "debugData", computeBuffers.debugBuffer);
        settings.spacialIndexerComputeShader.SetBuffer(kernels.PerCellCounter, "debugData", computeBuffers.debugBuffer);
        settings.BlellochScanComputeShader.SetBuffer(kernels.blellochScan, "debugData", computeBuffers.debugBuffer);
    }

    // Here we are putting all the kernel inits together along with all the data used by almost all kernels
    // such as the particleCount, the particles buffer and the bounds.
    static public void initKernelData(ParticleSettings settings, ref ComputeBuffers computeBuffers, ref Kernels kernels)
    {
        setMainKernelData(settings);
        initMainKernel(settings, ref kernels.densityKernel, ref kernels.mainKernel, computeBuffers);

        // #____Spacial Kernel INITS_____#
        InitSpacialIndexerKernels.initPerCellCounter(settings, ref kernels.PerCellCounter, computeBuffers);
        InitSpacialIndexerKernels.initBlellochSan(settings, ref kernels.blellochScan,
            computeBuffers.cellCounts, computeBuffers.blellochScanned, computeBuffers.scannedSums);
        InitSpacialIndexerKernels.initOffsetAddition(settings, ref kernels.sumOffsetAddition, computeBuffers);
        InitSpacialIndexerKernels.initIndexListFiller(settings, ref kernels.fillParticleIndexList, computeBuffers);
        InitSpacialIndexerKernels.initCleanUintKernel(settings, ref kernels.clearUintKernel, computeBuffers);
        InitSpacialIndexerKernels.initReorderParticles(settings, ref kernels.reorderParticles, computeBuffers);

        // setDebugBuffer(settings, kernels, computeBuffers);
    }
}