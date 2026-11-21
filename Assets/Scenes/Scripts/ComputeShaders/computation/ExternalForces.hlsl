#include "../../ParticleData.hlsl"

void addGravity(inout Particle currentParticle, float t)
{
    currentParticle.velocity.y -= gravityForce * t;
}
