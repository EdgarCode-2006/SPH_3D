using UnityEngine;
using SPH.Common;

public class DebugHelper
{
    static public void displayWireFrame(float boxWidth, Vector3 center, Material debugWireFrame)
    {
        GameObject cube = new GameObject("DebugWireCube");
        cube.transform.position = center;

        LineRenderer lr = cube.AddComponent<LineRenderer>();
        lr.material = debugWireFrame;
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.useWorldSpace = true;

        // 8 corners of the cube
        Vector3[] corners = new Vector3[8]
        {
            new Vector3(-boxWidth, -boxWidth, -boxWidth),
            new Vector3(boxWidth, -boxWidth, -boxWidth),
            new Vector3(boxWidth, boxWidth, -boxWidth),
            new Vector3(-boxWidth, boxWidth, -boxWidth),
            new Vector3(-boxWidth, -boxWidth, boxWidth),
            new Vector3(boxWidth, -boxWidth, boxWidth),
            new Vector3(boxWidth, boxWidth, boxWidth),
            new Vector3(-boxWidth, boxWidth, boxWidth)
        };

        // edges of the cube
        Vector3[] edges = {
            corners[0], corners[1], corners[1], corners[2], corners[2], corners[3], corners[3], corners[0],
            corners[4], corners[5], corners[5], corners[6], corners[6], corners[7], corners[7], corners[4],
            corners[0], corners[4], corners[1], corners[5], corners[2], corners[6], corners[3], corners[7]
        };

        lr.positionCount = edges.Length;
        lr.SetPositions(edges);
    }

    static public void printParticles(ParticleSettings settings, ComputeBuffer particleBuffer, int range = 5)
    {
        Particle[] particles = new Particle[settings.particleCount];

        particleBuffer.GetData(particles);
        range = (range > settings.particleCount) ? 5 : range;
        for (int i = 0; i < range; i++) // printing first 5 particle data
        {
            Debug.Log($"Particle {i}: density={particles[i].density} pressure={particles[i].pressure}");
        }
    }

    static public void printCellCounts(ParticleSettings settings, ComputeBuffer cellCountsBuffer, int range = 5)
    {
        uint[] sums = new uint[settings.cellCount];

        cellCountsBuffer.GetData(sums);
        range = settings.cellCount;
        // range = (range > settings.particleCount) ? 5 : range;
        for (int i = 0; i < range; i++)
        {
            if (sums[i] != 0)
            {
                Debug.Log("{" + i + "}" + "sum = " + sums[i]);
            }
        }
    }
    static public void printBlellochScanned(ParticleSettings settings, ComputeBuffer blellochScanned, int range = 5)
    {
        uint[] scanned = new uint[settings.cellCount + 1];

        blellochScanned.GetData(scanned);
        range = settings.cellCount + 1;
        // range = (range > settings.particleCount) ? 5 : range;
        for (int i = 0; i < range; i++)
        {
            Debug.Log("{" + i + "} " + "scanned = " + scanned[i]);
        }
    }

    static public void printUintBuffer(ComputeBuffer temp, string identifier , int range = 5)
    {
        uint[] scan = new uint[range];

        temp.GetData(scan);
        // range = (range > settings.particleCount) ? 5 : range;
        for (int i = 0; i < range; i++)
        {
            Debug.Log("{" + i + "} " + identifier + scan[i]);
        }
    }

    static public void printDebugBuffer(ComputeBuffer debugBuffer, ParticleSettings settings, int range = 5)
    {
        DebugStruct[] mortonCells = new DebugStruct[settings.particleCount];

        debugBuffer.GetData(mortonCells);
        range = (range > settings.particleCount) ? 5 : range;
        for (int i = 0; i < range; i++)
        {
            Debug.Log("{DEBUG} float4Value1 = " + mortonCells[i].float4Value1);
            if (mortonCells[i].floatValue1 > 0)
            {
                Debug.Log("{DEBUG} floatValue1 = " + mortonCells[i].floatValue1);
            }
            if (mortonCells[i].floatValue2 > 0)
            {
                Debug.Log("{DEBUG} floatValue2 = " + mortonCells[i].floatValue2);
            }
            Debug.Log("{DEBUG} uintValue1 = " + mortonCells[i].uintValue1);
            Debug.Log("{DEBUG} uintValue2 = " + mortonCells[i].uintValue2);
        }
    }
}



