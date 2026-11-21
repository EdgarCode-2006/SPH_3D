#include "../../ParticleData.hlsl"

void computeBoundCollision(inout Particle currentParticle)
{
    // X axis
    if (currentParticle.position.x + particleRadius >= boundX.y)
    {
        currentParticle.position.x = boundX.y - particleRadius;
        currentParticle.velocity.x = -abs(currentParticle.velocity.x) * collisionDampening;
    }
    if (currentParticle.position.x - particleRadius <= boundX.x) 
    {
        currentParticle.position.x = boundX.x + particleRadius;
        currentParticle.velocity.x = abs(currentParticle.velocity.x) * collisionDampening;
    }

    // Y axis
    if (currentParticle.position.y + particleRadius >= boundY.y) 
    {
        currentParticle.position.y = boundY.y - particleRadius;
        currentParticle.velocity.y = -abs(currentParticle.velocity.y) * collisionDampening;
    }
    if (currentParticle.position.y - particleRadius <= boundY.x) 
    {
        currentParticle.position.y = boundY.x + particleRadius;
        currentParticle.velocity.y = abs(currentParticle.velocity.y) * collisionDampening;
    }

    // Z axis
    if (currentParticle.position.z + particleRadius >= boundZ.y) 
    {
        currentParticle.position.z = boundZ.y - particleRadius;
        currentParticle.velocity.z = -abs(currentParticle.velocity.z) * collisionDampening;
    }
    if (currentParticle.position.z - particleRadius <= boundZ.x)
    {
        currentParticle.position.z = boundZ.x + particleRadius;
        currentParticle.velocity.z = abs(currentParticle.velocity.z) * collisionDampening;
    }
}