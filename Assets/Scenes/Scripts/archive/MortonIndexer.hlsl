
/*
* This function makes sure that the particle positions fall into the positive
* Z Ordered grid which starts at 0 and goes to maxBound - minBound for all directions
* of the cubic grid.
*/
uint computeGridIndex(float pos, uint cellsPerAxis, float minBound, float maxBound)
{
    // Normalize the position to fall into the positive grid plane
    // That the Z ordered curve covers
    float shifted = pos - minBound;                // move minBound → 0
    float range = maxBound - minBound;             // total extent
    float normalized = saturate(shifted / range);  // safety clamp between 0 & 1
    return (uint)(normalized * (cellsPerAxis - 1));
}

uint computeMortonIndex(uint posi, uint numbits)
{
    uint result = 0;

    for (uint i = 0; i < numbits; ++i) {
        uint bit = (posi >> i) & 1;
        result |= (bit << (3 * i));
    }
    return result;
}

uint mortonIndexer3D(float3 pos, float minBound, float maxBound, uint numbits)
{
    uint cellsPerAxis = (uint)pow(2.0, float(numbits));

    uint posxi = computeGridIndex(pos.x, cellsPerAxis, minBound, maxBound);
    uint posyi = computeGridIndex(pos.y, cellsPerAxis, minBound, maxBound);
    uint poszi = computeGridIndex(pos.z, cellsPerAxis, minBound, maxBound);
    
    return (computeMortonIndex(posxi, uint(numbits))) |
        (computeMortonIndex(posyi, uint(numbits)) << 1) |
        (computeMortonIndex(poszi, uint(numbits)) << 2);
}
