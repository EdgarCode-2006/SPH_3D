# SPH_3D: GPU Fluid Simulation in Unity

A 3D **Smoothed Particle Hydrodynamics (SPH)** fluid simulation for Unity, written in compute shaders. Every simulation step runs on the GPU: building the spatial grid, computing density and pressure, applying forces, integrating, and drawing the particles. The particle data never goes back to the CPU.

The pipeline is built to scale to around a million particles on a laptop GPU.

## 🚀 Features

* **Fully GPU simulation:** each step is a chain of about 10–12 compute dispatches, with no CPU readback.
* **Cell-sorted spatial grid:** a counting sort (per-cell count → Blelloch prefix sum → fill) rebuilt every step, so each particle only checks the 27 grid cells around it.
* **Cache-friendly data layout:** particle data is copied into grid-cell order every step and stored as one buffer per field (structure of arrays). Neighbour loops read memory in contiguous runs, with no index lookups.
* **One thread per particle:** threads run in cell order, so the threads of a GPU warp work on neighbouring particles and share their neighbour data.
* **Wall particles as boundaries:** the box is lined with fixed particles that build up density and pressure like the fluid does. The walls push back naturally, and particles no longer pile up in the corners.
* **Clean step order, no read/write races:** density for every particle is computed first, then forces and integration. Kernels read from sorted copies and write to separate buffers.
* **Lightweight rendering:** each particle is a camera-facing square (2 triangles) drawn as a shaded sphere in the fragment shader. Colour shows density: white spray to blue bulk fluid.

## 🧰 Requirements

* **Unity 2022.3.36f1** (Built-in render pipeline)
* **Windows / Direct3D 11** with a compute-capable GPU. Development and testing were on an NVIDIA RTX 4060 Laptop GPU.

## ▶️ Getting Started

1. Clone the repository and open the folder in Unity Hub with Unity 2022.3.36f1.
2. Open `Assets/Scenes/SampleScene.unity`.
3. Select `Assets/Scenes/Scripts/CSharp/ParticleSettings.asset` and pick a preset from the table below.
4. Press **Play**. The particles appear in their starting shape.
5. Press **Space** to start the simulation.

## ⚙️ Settings

Everything is set on the `ParticleSettings` asset.

| Field | Meaning | Change while playing |
|---|---|---|
| `particleCount` | Number of fluid particles. Wall particles are added on top. | Restart |
| `boxWidth` | **Half** the box size: the box spans `center ± boxWidth` on every axis. | Restart |
| `smoothingRadius` | SPH kernel radius `h`, which is also the grid cell size. Keep at **2**: the spawn spacing (1 unit) and the presets assume it. | Restart |
| `particleBoundCenter` | Centre of the box and of the starting shape. | Restart |
| `generatedShape` | Starting shape. Use **Cube**: Sphere and Torus spawn hollow shells. | Restart |
| `dampening` | **Pressure stiffness** `k` in `p = k (ρ − ρ0)`. Higher means less squashing, but needs a smaller `timeStep`. | Live |
| `timeStep` | Simulated seconds per step. One step runs per rendered frame. | Live |
| `restDensity` | Target density `ρ0`. Keep at about `particleMass × 1`. | Live |
| `particleMass` | Mass of each particle. | Live |
| `viscosityConstant` | Viscosity strength. Lower is splashier, higher is calmer. | Live |
| `gravityForce` | Gravity along −Y. | Live |
| `particleRadius` | Drawn radius, and the distance particles are kept from the box faces. | Live |
| `collisionDampening` | Bounce factor of the safety clamp at the box faces. | Live |

**Material (`ParticleMat`):** *Wall Particle Alpha* (0 hides the wall particles) and *Size Scale*, which changes the drawn size only.

**Compile-time switches** at the top of `ComputeMain.compute`:

| Switch | Default | Meaning |
|---|---|---|
| `USE_RK4` | `0` | `0` = semi-implicit Euler. `1` = per-particle RK4, about 4× the cost. |
| `MAX_SPEED` | `50.0` | Speed cap in units/s, as a safety net. |
| `MAX_NEIGHBORS_PER_ROW` | `384` | Cap on particles read per 3-cell neighbour row, to avoid GPU timeouts if particles pile up. |

### Presets

| | Quick (10k) | Main demo (100k) | Showpiece (1M) |
|---|---|---|---|
| `particleCount` | 10,000 | 100,000 | 1,000,000 |
| `boxWidth` | 15 | 35 | 80 |
| `dampening` | 1,000 | 2,000 | 4,000 |
| `timeStep` | 0.01 | 0.008 | 0.005 |
| `viscosityConstant` | 1.0 | 1.0 | 0.8 |
| Other fields | `particleRadius` 0.5, `smoothingRadius` 2, `restDensity` 1, `particleMass` 1, `gravityForce` 9.81, `collisionDampening` 0.3, Cube | same | same |

### Choosing your own values

1. **Fit:** `boxWidth ≥ cbrt(particleCount) / 2 + 1`. Otherwise the simulation refuses to start.
2. **Stiffness:** settled depth ≈ `particleCount / (2 × boxWidth)²`. Using `dampening ≈ 100 × depth` gives about 10% compression at the bottom.
3. **Time step:** impact speed ≈ `√(2 × 9.81 × fall height)`, then `timeStep ≈ 0.6 × 0.8 / (√dampening + impact speed)`. Larger values make the fluid jitter ("boil") or explode.

## 🛠️ How It Works

### Data

The particle state lives in separate GPU buffers, one per field:

| Buffer | Contents | Written by |
|---|---|---|
| `positions` | xyz = position, w = 1 for wall particles | `CSMain` |
| `velocities` | xyz = velocity | `CSMain` |
| `sortedPositions`, `sortedVelocities` | the same data copied into grid-cell order | `ReorderParticles` |
| `sortedDensityPressure` | x = density, y = pressure | `DensityPass` |
| `cellCounts`, `blellochScanned`, `particleIndexes` | the spatial grid | grid kernels |

The space is divided into cubic cells of size `h`. Every neighbour within `h` is then guaranteed to be in the 3×3×3 block of cells around a particle. The grid extends one wall thickness beyond the box so the wall particles get their own cells.

### One simulation step

| # | Kernel | What it does |
|---|---|---|
| 1 | `ClearUIntBuffer` | Resets the per-cell counts. |
| 2 | `PerCellCounter` | Each particle finds its cell and adds 1 to that cell's count (`InterlockedAdd`). |
| 3 | `BlellochScan` + `sumOffsetAddition` | A prefix sum over the counts gives each cell's start index in the sorted list. |
| 4 | `FillParticleIndexList` | Each particle claims a unique place inside its cell (`InterlockedAdd`) and writes its index into `particleIndexes`. |
| 5 | `ClearUIntBuffer` | Resets the per-cell write offsets. |
| 6 | `ReorderParticles` | Copies positions and velocities into cell order: `sorted[s] = state[particleIndexes[s]]`. |
| 7 | `DensityPass` | Density and pressure for every particle, fluid and walls. |
| 8 | `CSMain` | Forces, integration and box clamp for the fluid. The new state is written to `positions` / `velocities`. |

Cell `c` holds slots `[cellStarts[c], cellStarts[c] + cellCounts[c])`. The three cells along x in a row sit next to each other in memory, so the 27 neighbour cells are read as **9 contiguous runs**. The neighbour loops read only the position of each candidate, and fetch velocity, density and pressure only for neighbours within `h`.

Each thread writes its own slot, so the state buffers come out of a step already in cell order. The next step's sort then reads them almost sequentially.

### Physics

The kernels and forces follow Müller et al. (2003).

Density (Poly6 kernel, the particle itself included):

$$\rho_i = \sum_j m \, \frac{315}{64\pi h^9}\left(h^2 - r_{ij}^2\right)^3$$

Pressure (equation of state, never negative):

$$p_i = \max\left(0,\; k\,(\rho_i - \rho_0)\right)$$

Pressure acceleration (spiky kernel gradient):

$$\mathbf{a}^{p}_i = -\sum_j m \left(\frac{p_i}{\rho_i^2} + \frac{p_j}{\rho_j^2}\right) \nabla W_{spiky}, \qquad \nabla W_{spiky} = -\frac{45}{\pi h^6}(h - r)^2\,\hat{\mathbf{r}}_{ij}$$

Viscosity acceleration (viscosity kernel Laplacian):

$$\mathbf{a}^{v}_i = \mu \sum_j m \,\frac{\mathbf{v}_j - \mathbf{v}_i}{\rho_j}\,\frac{45}{\pi h^6}(h - r)$$

**Integration:** semi-implicit Euler, `v += a·dt`, then `x += v·dt`. An optional RK4 path moves only the particle itself through its stages while its neighbours stay fixed.

**Boundaries:** the box is lined with fixed wall particles in a lattice at the fluid spacing, `ceil(h)` layers thick. They compute their own density and pressure every step (a dynamic boundary, Crespo et al. 2007), so fluid pressed into a wall is pushed back. A hard clamp at the box faces catches any particle fast enough to get through.

### Rendering

`Graphics.RenderPrimitives` draws 6 vertices per particle. The vertex shader builds a square facing the camera. The fragment shader cuts it into a circle and lights it like a sphere, so it looks round. Colour runs from white (low density: spray, free surface) to blue (rest density: bulk fluid). Wall particles are collapsed to nothing unless *Wall Particle Alpha* is above 0.

## 📁 Project Structure

```
Assets/Scenes/
├── SampleScene.unity
├── Materials/ParticleMat.mat
└── Scripts/
    ├── ParticleData.hlsl            shared struct and simulation constants
    ├── CSharp/
    │   ├── SimMain.cs               MonoBehaviour entry point (Space starts the simulation)
    │   ├── KernelManager.cs         grid setup, per-step dispatches, draw call
    │   ├── ParticleSettings.asset   simulation settings
    │   ├── Common/                  settings class, buffer and kernel structs
    │   ├── Inits/                   particle and wall generation, buffer and kernel setup
    │   └── KernelDispatch/          prefix-sum and main-kernel dispatch
    ├── ComputeShaders/
    │   ├── SpacialIndexer.compute   count, fill and reorder kernels
    │   ├── BlellochScan.compute     prefix sum
    │   ├── ComputeMain.compute      DensityPass and CSMain
    │   ├── computation/             kernels, pressure, gravity, box clamp
    │   └── optimization/            position → grid cell
    ├── Shaders/ParticleShader.shader
    ├── Doc/SpacialHash.md           notes on the spatial grid
    └── archive/                     earlier Morton-code / bitonic-sort experiments
```

## ⚠️ Known Limitations

* **One step per rendered frame.** Small time steps therefore play back in slow motion.
* **Cube box only.** `boxWidth` sets all three axes, and the starting shape is centred in the box.
* **Hollow Sphere and Torus spawns.** These shapes generate shells, not filled volumes.
* **Windows / Direct3D 11 only.** Other graphics APIs are untested.
* **Particle limit per dispatch:** about 16.7 million (65,535 thread groups × 256 threads).
* **Settings stick after Play mode.** `ParticleSettings` is a ScriptableObject, so changes made in Play mode are not reverted when you stop.

## 📚 References

* M. Müller, D. Charypar, M. Gross, *Particle-Based Fluid Simulation for Interactive Applications*, SCA 2003.
* A. J. C. Crespo, M. Gómez-Gesteira, R. A. Dalrymple, *Boundary Conditions Generated by Dynamic Particles in SPH Methods*, 2007.
* M. Harris, S. Sengupta, J. D. Owens, *Parallel Prefix Sum (Scan) with CUDA*, GPU Gems 3, Chapter 39, 2007.
