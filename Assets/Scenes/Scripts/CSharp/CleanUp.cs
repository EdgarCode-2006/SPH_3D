using UnityEngine;
using SPH.Common;

/*
* This class holds all the cleanup logic of the project in order for
* the data inside of the compute buffers allocated on the GPU to be safely
* "released" or freed.
*/
static public class CleanUp
{
    static public void releaseBuffer(ref ComputeBuffer buffer)
    {
        if (buffer != null)
        {
            buffer.Release();
            buffer = null;
        }
    }

    /*
    * called at the end of the program it cleans up the resources
    * by freeing the allocated buffer space inside of the GPU.
    */
    static public void Dispose(ref ComputeBuffers computeBuffers)
    {
        releaseBuffer(ref computeBuffers.positions);
        releaseBuffer(ref computeBuffers.velocities);
        releaseBuffer(ref computeBuffers.sortedPositions);
        releaseBuffer(ref computeBuffers.sortedVelocities);
        releaseBuffer(ref computeBuffers.sortedDensityPressure);
        releaseBuffer(ref computeBuffers.cellCounts);
        // releaseBuffer(ref computeBuffers.debugBuffer);
        releaseBuffer(ref computeBuffers.blellochScanned);
        releaseBuffer(ref computeBuffers.scannedSums);
        releaseBuffer(ref computeBuffers.particleIndexes);
        releaseBuffer(ref computeBuffers.cellOffsets);
    }

}