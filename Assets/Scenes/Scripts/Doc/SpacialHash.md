# GPU Spatial Hashing for Large-Scale Particle Simulation

This system organizes particles into a **fixed 3D grid on the GPU** to make neighbor queries fast and scalable. Instead of checking every particle against every other particle, we only compare particles that live in the same or nearby grid cells — turning an expensive global problem into a local one.

The grid is **static and uniform**, which is sufficient for real-time particle simulation and avoids the complexity of hierarchical spatial structures (like octrees or Z-order curves) that provide little benefit for densely populated, constantly moving particles.

## Overview of the Pipeline
The spatial hash is built in **three GPU phases** every frame:
1. Count how many particles fall into each cell
2. Convert those counts into offsets using a prefix sum (Blelloch scan)
3. Use the offsets to build a compact particle index list, grouped by cell
Once complete, each grid cell has:
- a **start index** into the particle index buffer
- a **particle count**, defining a contiguous range
This layout allows fast neighbor queries with coalesced memory access.

## 1. Cell Counting Phase (Per-Particle)
Each particle is processed by one GPU thread.
- The particle’s 3D position is converted into a 1D cell index.
- The thread performs an **atomic increment** in a cellCounts buffer at that index.
This produces a histogram:
```bash
cellCounts[cellIndex] = number of particles in that cell
```
Atomic operations are safe here because each thread only increments a single integer, and contention is naturally limited by spatial locality.
**Result:** a dense array where each entry tells us how many particles live in a given cell — but not where they should go in memory.

## 2. Prefix Sum Phase (Blelloch Scan)
Counting particles isn’t enough — we need to know **where each cell’s particles begin** inside a compact index buffer.
This is solved with a prefix sum, which converts counts into offsets.
### What the prefix sum gives us
if we compute
```bash
cellStarts[i] = sum(cellCounts[0..i-1])
```
Then:
- ``cellStarts[i]`` is the **start offset** of cell ``i``
- ``cellCounts[i]`` is the size of that cell
- Together, they define the range:
```bash
(cellStarts[i], cellStarts[i] + cellCounts[i])
```
### Parallel Prefix Sum on the GPU
Because the grid can contain tens or hundreds of thousands of cells, the prefix sum itself must be parallel.

This implementation uses a **block-based Blelloch scan**:
1. The cellCounts array is split into blocks of 256 elements
2. Each thread group:
    - loads its block
    - computes a local prefix sum
    - writes the block’s total sum into a separate ```blockSums``` buffer
3. The ``blockSums`` buffer is recursively scanned
4. The resulting offsets are added back into each block
This two-level scan allows the entire array to be prefix-summed efficiently while maintaining good GPU occupancy and memory coherence.
**Result**: a ``cellStarts`` buffer that maps each cell to a contiguous region of the particle index buffer.

## 3. Particle Index Construction Phase\
Now that we know **where each cell begins**, we rebuild the particle list.

Each particle:
- recomputes its cell index
- atomically increments a per-cell write offset
- writes its own index into the correct location in ``particleIndices``
The result is a compact array where particles belonging to the same cell are stored contiguously.

## Why This Works Well on the GPU
- **Massively parallel**: one thread per particle where possible
- **Memory-coherent**: particle data is accessed in contiguous ranges
- **Scales linearly** with particle count
- **Avoids branching and recursion** in the main simulation kernels
This structure is simple, predictable, and maps cleanly to GPU hardware.