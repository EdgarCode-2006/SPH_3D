using UnityEngine;

static public class DispatchMain
{
    // D3D11 limit on thread groups per dispatch dimension.
    const int MAX_THREAD_GROUP_COUNT = 65535;

    // DensityPass: density + pressure of every particle (fluid + walls).
    static public void dispatchDensityKernel(ParticleSettings settings, int densityKernel)
    {
        dispatchOneThreadPerParticle(settings, densityKernel);
    }

    // CSMain: forces + integration of every fluid particle (walls are copied through).
    static public void dispatchMainKernel(ParticleSettings settings, int mainKernel)
    {
        dispatchOneThreadPerParticle(settings, mainKernel);
    }

    /*
    * Both kernels run one thread per particle (fluid + walls), 256 threads per group.
    * One dispatch covers up to 65535 * 256 = ~16.7 million particles.
    */
    static private void dispatchOneThreadPerParticle(ParticleSettings settings, int kernel)
    {
        int groupCount = Mathf.CeilToInt((float)settings.totalParticleCount / settings.blockSize);
        groupCount = Mathf.Max(1, groupCount);

        if (groupCount > MAX_THREAD_GROUP_COUNT)
        {
            Debug.LogError($"[SPH] {settings.totalParticleCount} particles need {groupCount} thread groups, " +
                           $"more than the {MAX_THREAD_GROUP_COUNT} allowed in one dispatch.");
            return;
        }

        settings.mainComputeShader.Dispatch(kernel, groupCount, 1, 1);
    }
}
