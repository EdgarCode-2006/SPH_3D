using UnityEngine;

/*
* This class is used Bitonic sort which we use to sort the MortonPairs by
* MortonCodes which facilitates the mortonPairs grouping for mortonCells.
* This kernel is initialized in a seperate class for clarity and because it
* also a seperate program from the main SPH simulation as it is only a sorting
* algorithm.
*/
class BitonicSort
{
    // descending would just be !ASCENDING but we will not be using it.
    static public int ASCENDING = 1;

    /*
    * This function is just used to set the kernel for the Bitonic Sort which
    * resides in a different file than the one that holds all the other kernels.
    */
    // static public int setKernel(ParticleSettings settings, ComputeBuffer mortonPairBuffer)
    // {
    //     int sortKernel = settings.BitonicSortComputeShader.FindKernel("CSBitonic_Sort");
    //     Debug.Log("Sorting kernel index = " + sortKernel);
    //     settings.BitonicSortComputeShader.SetBuffer(sortKernel, "mortonPairs", mortonPairBuffer);
    //     settings.BitonicSortComputeShader.SetInt("particleCount", settings.particleCount);
    //     settings.BitonicSortComputeShader.SetInt("k", 0);
    //     settings.BitonicSortComputeShader.SetInt("j", 0);
    //     settings.BitonicSortComputeShader.SetInt("dir", ASCENDING);

    //     return sortKernel;
    // }

    // /*
    // * For bitonic sort we use the CPU and GPU together which makes the process clearer.
    // * The tracking of the grouping indexes and the direction to sort each bitonic sequence is
    // * stored and generated on the CPU which is transfered to the GPU every iteration in order
    // * to sort each sequence concurently. This ensures that we are taking advantage of the GPU
    // * and not doing everything fully on the CPU which would increase overhead as the data
    // * would need to be transfered back and forth from the GPU to CPU and calculation wouldn't be
    // * concurent.
    // */
    // static public void bitonicSort(ParticleSettings settings, int threadGroupCount, int sortKernel)
    // {
    //     // Outer loop builds sequences of size k
    //     for (int k = 2; k <= settings.particleCount; k *= 2)
    //     {
    //         // Inner loop defines distance j for comparison
    //         for (int j = k / 2; j > 0; j /= 2)
    //         {
    //             settings.BitonicSortComputeShader.SetInt("k", k);
    //             settings.BitonicSortComputeShader.SetInt("j", j);
    //             settings.BitonicSortComputeShader.Dispatch(sortKernel, threadGroupCount, 1, 1);
    //         }
    //     }

    // }
}