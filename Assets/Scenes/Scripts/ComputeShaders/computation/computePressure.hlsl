#include "../../ParticleData.hlsl"

float computePressure(float currentParticleDensity)
{
    return max(0.0, dampening * (currentParticleDensity - restDensity));
}