#include "../../ParticleData.hlsl"

uint CompactBits(uint x) {
    x &= 0x1249249;   // mask 0b001001001001001001001001001
    x = (x ^ (x >> 2)) & 0x30c30c3;
    x = (x ^ (x >> 4)) & 0xf00f00f;
    x = (x ^ (x >> 8)) & 0x00ff000f;
    x = (x ^ (x >> 16)) & 0x0000ffff;
    return x;
}

uint3 DecodeMortonBits(uint code) {
    return uint3(CompactBits(code >> 0),
                 CompactBits(code >> 1),
                 CompactBits(code >> 2));
}

bool isAdjacent(uint currentMortonCode, uint mortonCode_j)
{
    if (currentMortonCode == mortonCode_j) {
        return true;
    }
    uint3 a = DecodeMortonBits(currentMortonCode);
    uint3 b = DecodeMortonBits(mortonCode_j);
    // compare the distances in order to see if the mortonCodes are
    // adjacent.
    return abs(int(a.x) - int(b.x)) <= smoothingRadius &&
       abs(int(a.y) - int(b.y)) <= smoothingRadius &&
       abs(int(a.z) - int(b.z)) <= smoothingRadius;
}
