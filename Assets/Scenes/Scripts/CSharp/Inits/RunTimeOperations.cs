using SPH.Common;
using UnityEngine;

/*
* This function is used to reset the persistant buffers in the sim so that
* we can use empty memory per frame. This allows for stateless memory per frame.
* (mainly used for MortonCellIndexer refreshes.)
*/
static public class RuntimeOperations
{
    static public void clearBuffer(ComputeBuffer bufferToClear, int size, ParticleSettings settings, Kernels kernels)
    {
        // cast to float first: int / int truncates before CeilToInt can round up.
        int threadGroupCount = Mathf.CeilToInt((float)size / settings.blockSize);
        threadGroupCount = Mathf.Max(1, threadGroupCount);
        
        settings.spacialIndexerComputeShader.SetBuffer(kernels.clearUintKernel, "cellCounts", bufferToClear);
        settings.spacialIndexerComputeShader.SetInt("elementCount", size);
        settings.spacialIndexerComputeShader.Dispatch(kernels.clearUintKernel, threadGroupCount, 1, 1);
    }
}