#include "../../ParticleData.hlsl"

static const float pi = 3.141592;

// Below this distance two particles count as coincident: the direction between them
// (rij / r) is undefined, and r = 0 would give 0/0 = NaN.
static const float MIN_DISTANCE = 1e-6;

/*
* True when x is a normal number (not NaN or +/-inf). Done with an integer bit test on
* purpose: the HLSL compiler is allowed to rewrite float comparisons as if NaN can't
* exist (e.g. turning !(r >= 0) into r < 0), which can silently let NaN through.
* Integer operations can't be rewritten that way. A float is NaN/inf when all exponent
* bits (0x7F800000) are set.
*/
bool isFiniteFloat(float x)
{
    return (asuint(x) & 0x7F800000) != 0x7F800000;
}

// 0 <= r <= h, and r is a real number. A NaN distance (from a NaN particle) used to
// pass the old check because every comparison with NaN is false, which let one bad
// particle poison all of its neighbours.
bool isDistanceRValid(float distance_r)
{
    return isFiniteFloat(distance_r) && distance_r >= 0 && distance_r <= smoothingRadius;
}

float polySixKernel(float distance_r)
{
    if (! isDistanceRValid(distance_r)) return 0.0;

    float distFromOrigin = ((smoothingRadius * smoothingRadius) - (distance_r * distance_r));
    distFromOrigin = distFromOrigin * distFromOrigin * distFromOrigin;

    return smoothingStretch * distFromOrigin;
}

float viscosityKernel(float distance_r)
{
    if (! isDistanceRValid(distance_r)) return 0.0;

    float distFromOrigin = (smoothingRadius - distance_r);

    return smoothingScalar * distFromOrigin;
}

float spkiyKernel(float distance_r)
{
    if (! isDistanceRValid(distance_r)) return 0.0;
    return spikyKernelScalar * pow(smoothingRadius - distance_r, 2);
}

float3 spkiyKernelGrad(float3 rij)
{
    float distance_r = length(rij);

    // coincident particles (e.g. two particles clamped into the same box corner):
    // no defined direction, so no force instead of rij / 0 = NaN.
    if (! isDistanceRValid(distance_r) || distance_r < MIN_DISTANCE) return float3(0,0,0);
    return spikyKernelScalar * pow(smoothingRadius - distance_r, 2) * (rij / distance_r);
}

float3 viscosityKernelGrad(float3 rij)
{
    float distance_r = length(rij);

    if (! isDistanceRValid(distance_r) || distance_r < MIN_DISTANCE) return float3(0,0,0);
    return smoothingScalar * pow(smoothingRadius - distance_r, 2) * (rij / distance_r);
}