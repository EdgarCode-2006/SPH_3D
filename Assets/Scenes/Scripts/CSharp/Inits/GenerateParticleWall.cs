using System.Collections.Generic;
using UnityEngine;

/*
* Builds the fixed wall particles that enclose the simulation box.
*
* The walls are one regular lattice filling a shell around the box: every lattice point
* that lies ON or OUTSIDE a wall plane is a wall particle, every point strictly inside
* the box is left for the fluid. Because it is a single lattice, faces, edges and corners
* are each filled exactly once: no doubled particles on one side, no gaps on the other.
*
* Layout along one axis (box from min to max, L = wallLayers):
*
*     wall layers          fluid region            wall layers
*   x  x  x  x  |  .  .  .  .  .  .  .  .  .  |  x  x  x  x
*  min-(L-1)s  min                           max          max+(L-1)s
*
* The first layer sits exactly on each wall plane, the rest go outwards.
*/
static public class BoundGeneration
{
    // Same spacing as the fluid's Cube spawn, so a fluid particle next to a wall sees the
    // same neighbour density on the wall side as it does inside the fluid.
    public const float WALL_SPACING = InitBuffers.CUBE_SPACING;

    /*
    * Number of layers, so the walls fill the whole smoothing kernel of a fluid particle
    * sitting on the wall plane. Lattice points exactly smoothingRadius away contribute 0
    * to the kernel, so ceil(h / spacing) layers is enough (2 layers for h = 2).
    */
    static public int wallLayers(ParticleSettings settings)
    {
        return Mathf.Max(1, Mathf.CeilToInt(settings.smoothingRadius / WALL_SPACING));
    }

    // Lattice steps across a box width. The spacing is stretched very slightly (width / steps)
    // so a layer lands exactly on both wall planes and the walls are symmetric.
    static private int stepsAcross(float width)
    {
        return Mathf.Max(1, Mathf.RoundToInt(width / WALL_SPACING));
    }

    // How far the wall layers reach outside the box, measured from the wall plane.
    static public float wallThickness(ParticleSettings settings)
    {
        float width = settings.boxWidth * 2f;
        float spacing = width / stepsAcross(width);
        return (wallLayers(settings) - 1) * spacing;
    }

    // Half width of the spatial grid: the box plus the wall layers outside it, so every
    // wall particle gets its own grid cell instead of being clamped into the edge cells.
    static public float gridHalfWidth(ParticleSettings settings)
    {
        return settings.boxWidth + wallThickness(settings);
    }

    static public List<Vector4> GenerateParticleBound(Vector2 boundX, Vector2 boundY, Vector2 boundZ, ParticleSettings settings)
    {
        int layers = wallLayers(settings);

        int nx = stepsAcross(boundX.y - boundX.x);
        int ny = stepsAcross(boundY.y - boundY.x);
        int nz = stepsAcross(boundZ.y - boundZ.x);

        float sx = (boundX.y - boundX.x) / nx;
        float sy = (boundY.y - boundY.x) / ny;
        float sz = (boundZ.y - boundZ.x) / nz;

        // Index 0 is the min wall plane and n is the max wall plane on each axis.
        // Indices 1..n-1 are inside the box; the extra layers go to -(layers-1) and n+(layers-1).
        int first = -(layers - 1);
        int lastX = nx + layers - 1;
        int lastY = ny + layers - 1;
        int lastZ = nz + layers - 1;

        // shell = whole lattice block minus the fluid interior
        int capacity = (lastX - first + 1) * (lastY - first + 1) * (lastZ - first + 1)
                     - Mathf.Max(0, nx - 1) * Mathf.Max(0, ny - 1) * Mathf.Max(0, nz - 1);
        List<Vector4> positions = new List<Vector4>(capacity);

        for (int k = first; k <= lastZ; k++)
        {
            bool kInside = k > 0 && k < nz;
            for (int j = first; j <= lastY; j++)
            {
                bool jInside = j > 0 && j < ny;
                for (int i = first; i <= lastX; i++)
                {
                    bool iInside = i > 0 && i < nx;
                    if (iInside && jInside && kInside)
                        continue; // inside the box: belongs to the fluid

                    positions.Add(new Vector4(
                        boundX.x + i * sx,
                        boundY.x + j * sy,
                        boundZ.x + k * sz,
                        1f));
                }
            }
        }
        return positions;
    }
}
