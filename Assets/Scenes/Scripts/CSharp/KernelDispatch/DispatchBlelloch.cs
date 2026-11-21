using UnityEngine;
using SPH.Common;

static public class DispatchBlelloch
{
    struct blellochScanInputs
    {
        public ComputeBuffer inputBuffer;
        public ComputeBuffer outputBuffer;
        public ComputeBuffer outputSumsBuffer;
        public int elementCount;

    }

    static private blellochScanInputs prepScanInputs(
        ComputeBuffer inputBuffer, ComputeBuffer outputBuffer, ComputeBuffer outputSumsBuffer, int elementCount
    )
    {
        blellochScanInputs result = new blellochScanInputs();

        result.inputBuffer = inputBuffer;
        result.outputBuffer = outputBuffer;
        result.outputSumsBuffer = outputSumsBuffer;
        result.elementCount = elementCount;

        return result;
    }

    static private void attachScanIOBuffers(blellochScanInputs inputs, ComputeShader blellochScanShader, int blellochScanKernel)
    {
        blellochScanShader.SetBuffer(blellochScanKernel, "elements", inputs.inputBuffer);
        blellochScanShader.SetBuffer(blellochScanKernel, "blellochScanned", inputs.outputBuffer);
        blellochScanShader.SetBuffer(blellochScanKernel, "scannedSums", inputs.outputSumsBuffer);

        blellochScanShader.SetInt("elementCount", inputs.elementCount);
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
    static private ComputeBuffer initNextSums(int size)
    {
        var buffer = new ComputeBuffer(size, sizeof(uint));

        // Only copy the portion you need; the rest is zero
        buffer.SetData(zero256k, 0, 0, size);
        return buffer;
    }

    static private int fetchThreadGroupCount(float cellCount, int blockSize)
    {
        int threadGroupCount = Mathf.CeilToInt((float)cellCount / blockSize);
        return Mathf.Max(1, threadGroupCount);
    }

// input =  computeBuffers.scannedSums
    static private void dispatchScan(ComputeBuffer inputComputeBuffer, ParticleSettings settings, Kernels kernels, int threadGroupCount)
    {
        // get the initial bellochScan of the CellCounts
        settings.BlellochScanComputeShader.Dispatch(kernels.blellochScan, threadGroupCount, 1, 1);
    }

    static private void sumOffsetAddition(ParticleSettings settings, int offsetAdditionKernel, int threadGroupCount,
        ComputeBuffer offsettingSums, ComputeBuffer blellochScanned)
    {
        settings.BlellochScanComputeShader.SetBuffer(offsetAdditionKernel, "scannedSums", offsettingSums);
        settings.BlellochScanComputeShader.SetBuffer(offsetAdditionKernel, "blellochScanned", blellochScanned);
        settings.BlellochScanComputeShader.Dispatch(offsetAdditionKernel, threadGroupCount, 1, 1);
    }

    static private void blellochScanBufferCleanup(ref ComputeBuffer scannedBuffer, ref ComputeBuffer sumsBuffer)
    {
        CleanUp.releaseBuffer(ref scannedBuffer);
        CleanUp.releaseBuffer(ref sumsBuffer);
    }

    static public void blellochScanRecursive(
        ParticleSettings settings,
        Kernels kernels,
        int elementCount,
        ComputeBuffer input,
        ComputeBuffer output
    )
    {
        int blockSize = settings.blockSize;
        int numBlocks = Mathf.CeilToInt((float)elementCount / blockSize);

        // Buffer for per-block sums
        ComputeBuffer blockSums = new ComputeBuffer(numBlocks, sizeof(uint));

        // ---- Level scan ----
        settings.BlellochScanComputeShader.SetInt("elementCount", elementCount);
        settings.BlellochScanComputeShader.SetBuffer(kernels.blellochScan, "elements", input);
        settings.BlellochScanComputeShader.SetBuffer(kernels.blellochScan, "blellochScanned", output);
        settings.BlellochScanComputeShader.SetBuffer(kernels.blellochScan, "scannedSums", blockSums);
        settings.BlellochScanComputeShader.Dispatch(kernels.blellochScan, numBlocks, 1, 1);

        // ---- Recurse if needed ----
        if (numBlocks > 1)
        {
            ComputeBuffer scannedBlockSums =
                new ComputeBuffer(numBlocks, sizeof(uint));

            blellochScanRecursive(
                settings,
                kernels,
                numBlocks,
                blockSums,
                scannedBlockSums
            );

            // ---- Offset-add ----
            settings.BlellochScanComputeShader.SetBuffer(
                kernels.sumOffsetAddition, "scannedSums", scannedBlockSums);
            settings.BlellochScanComputeShader.SetBuffer(
                kernels.sumOffsetAddition, "blellochScanned", output);
            settings.BlellochScanComputeShader.Dispatch(
                kernels.sumOffsetAddition, numBlocks, 1, 1);

            scannedBlockSums.Release();
        }

        blockSums.Release();
    }
}