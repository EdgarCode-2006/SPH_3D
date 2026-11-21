namespace SPH.Common
{
    using UnityEngine;

    public struct Particle
    {
        public Vector4 position;
        public Vector4 velocity;
        public Vector4 buffer2;
        public float density;
        public float pressure;
        // 1 = fixed wall particle (only contributes to the neighbour sums), 0 = fluid.
        // Replaces the old unused "buffer" padding float, so the stride is unchanged.
        public uint isBoundary;
        public uint mortonCode;
    }

    // This is the struct that holds all the compute buffers used by
    // the kernels inside of the main compute shader (for organization).
    public struct ComputeBuffers
    {
        /*
        * Particle data, one buffer per field (see PARTICLE DATA in ComputeMain.compute).
        *   positions / velocities: state at the end of the step (CSMain writes, the
        *       grid kernels and the renderer read). position.w = 1 for walls.
        *   sorted*: copy in grid-cell order made by ReorderParticles each step, read by
        *       DensityPass and CSMain. sortedDensityPressure is written by DensityPass.
        */
        public ComputeBuffer positions;
        public ComputeBuffer velocities;
        public ComputeBuffer sortedPositions;
        public ComputeBuffer sortedVelocities;
        public ComputeBuffer sortedDensityPressure;

        public ComputeBuffer debugBuffer;
        public ComputeBuffer cellCounts;
        public ComputeBuffer blellochScanned;
        public ComputeBuffer scannedSums;
        public ComputeBuffer particleIndexes;
        public ComputeBuffer cellOffsets;
    }

    // This is the struct that holds all the kernel references in the main
    // comput shader (for organization).
    public struct Kernels
    {
        public int densityKernel;
        public int mainKernel;
        public int reorderParticles;
        public int PerCellCounter;
        public int blellochScan;
        public int sumOffsetAddition;
        public int fillParticleIndexList;
        public int clearUintKernel;
    }

    /*
    * This buffer struct holds a variety of different data types which
    * are loaded inside of the GPU mem in order to set values during compute
    * shader execution that we can read back to the CPU in order to get
    * insights on the data modification inside of the GPU. This is necessary
    * since the GPU isnt capable of logging any outputs directly to the console.
    */
    struct DebugStruct
    {
        public Vector4 float4Value1;
        public float floatValue1;
        public float floatValue2;
        public uint uintValue1;
        public uint uintValue2;
    };
}
