#include "../../ParticleData.hlsl"

// 3D grid cell (x, y, z) that contains a position, clamped into the grid.
int3 PositionToCell(float3 position)
{
    // The grid starts at gridOrigin (not the box corner) so the wall layers outside the
    // box fall in their own cells instead of being clamped into the edge cells.
    float3 rel = (position - gridOrigin) / smoothingRadius;
    // Clamp while the value is still signed: casting a negative float to uint first
    // wraps it to a huge number, which would then clamp to the opposite wall's cell.
    int3 cell = (int3)floor(rel);
    return clamp(cell, int3(0, 0, 0), int3(resolution) - 1);
}

// Flat cell index: x + y * res.x + z * res.x * res.y. Neighbouring x cells are consecutive.
uint CellToIndex(int3 cell)
{
    return (uint)cell.x +
           (uint)cell.y * resolution.x +
           (uint)cell.z * resolution.x * resolution.y;
}

uint PositionToIndex(float3 position)
{
    return CellToIndex(PositionToCell(position));
}
