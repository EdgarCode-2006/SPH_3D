#ifndef PARTICLE_COMMON_INCLUDED
#define PARTICLE_COMMON_INCLUDED

/*
* One particle, as the kernels work with it locally. On the GPU the data is NOT stored as
* an array of this struct: it lives in separate buffers (positions with the wall flag in
* w, velocities, density + pressure), see the PARTICLE DATA comment in ComputeMain.compute.
* Kernels load a slot into this struct (loadSortedParticle) and store it back.
*/
struct Particle
{
    float4 position;
    float4 velocity;
    float4 buffer2;
    float density;
    float pressure;
    uint isBoundary;   // 1 = fixed wall particle, 0 = fluid (must match DataTypes.cs)
    uint mortonCode;
};

struct DebugStruct
{
    float4 float4Value1;
    float floatValue1;
    float floatValue2;
    uint uintValue1;
    uint uintValue2;
};

struct forcesData
{
    float3 pressure;
    float3 viscosity;
};

// Simulation parameters
uint particleCount;   // fluid + wall particles (settings.totalParticleCount)
float particleRadius;
float gravityForce;
float collisionDampening;
float dampening;

float restDensity;
float timeStep;
float smoothingRadius;
float viscosityConstant;
float particleMass;

float2 boundX;
float2 boundY;
float2 boundZ;
float3 gridOrigin;    // min corner of the spatial grid = box min corner minus the wall thickness
uint3 resolution;

uint elementCount;
uint cellCount;
uint BLOCK_SIZE;

float smoothingStretch;
float smoothingScalar;
float spikyKernelScalar;

#endif
