using SPH.Common;
using UnityEngine;

static public class InitSpacialIndexerKernels
{
    static public void initOffsetAddition(ParticleSettings settings, ref int sumOffsetAddition, ComputeBuffers computeBuffers)
    {
        sumOffsetAddition = settings.BlellochScanComputeShader.FindKernel("sumOffsetAddition");
    }

    static public void initIndexListFiller(ParticleSettings settings, ref int fillIndexList, ComputeBuffers computeBuffers)
    {
        fillIndexList = settings.spacialIndexerComputeShader.FindKernel("FillParticleIndexList");
        
        settings.spacialIndexerComputeShader.SetBuffer(fillIndexList, "particleIndexes", computeBuffers.particleIndexes);
        settings.spacialIndexerComputeShader.SetBuffer(fillIndexList, "positions", computeBuffers.positions);
    }

    // Copies positions / velocities into cell order (the sorted buffers) using particleIndexes.
    static public void initReorderParticles(ParticleSettings settings, ref int reorderParticles, ComputeBuffers computeBuffers)
    {
        ComputeShader shader = settings.spacialIndexerComputeShader;
        reorderParticles = shader.FindKernel("ReorderParticles");

        shader.SetBuffer(reorderParticles, "particleIndexes", computeBuffers.particleIndexes);
        shader.SetBuffer(reorderParticles, "positions", computeBuffers.positions);
        shader.SetBuffer(reorderParticles, "velocities", computeBuffers.velocities);
        shader.SetBuffer(reorderParticles, "sortedPositions", computeBuffers.sortedPositions);
        shader.SetBuffer(reorderParticles, "sortedVelocities", computeBuffers.sortedVelocities);
    }

    static public void initCleanUintKernel(ParticleSettings settings, ref int clearKernel, ComputeBuffers computeBuffers)
    {
        clearKernel = settings.spacialIndexerComputeShader.FindKernel("ClearUIntBuffer");
    }

    static public void initBlellochSan(ParticleSettings settings, ref int blellochScan,
        ComputeBuffer elements, ComputeBuffer blellochScanned, ComputeBuffer scannedSums)
    {
        blellochScan = settings.BlellochScanComputeShader.FindKernel("BlellochScan");
        settings.BlellochScanComputeShader.SetInt("BLOCK_SIZE", settings.blockSize);

    }
    static public void initPerCellCounter(
        ParticleSettings settings,
        ref int perCellCounter,
        ComputeBuffers computeBuffers)
    {
        perCellCounter = settings.spacialIndexerComputeShader.FindKernel("PerCellCounter");
        settings.spacialIndexerComputeShader.SetBuffer(perCellCounter, "positions", computeBuffers.positions);
        settings.spacialIndexerComputeShader.SetBuffer(perCellCounter, "cellCounts", computeBuffers.cellCounts);
    }
}
