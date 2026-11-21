#include "../ParticleData.hlsl"

DebugStruct initDebugStruct(inout DebugStruct debugCell)
{
    debugCell = (DebugStruct)0;
    
    debugCell.float4Value1.xyz = 0;
    debugCell.floatValue1 = 0;
    debugCell.floatValue2 = 0;
    debugCell.uintValue1 = 0;
    debugCell.uintValue2 = 0;
    return debugCell;
}

MortonPair initMortonPairStruct(uint mortonCode, uint index)
{
    MortonPair mortonPair = (MortonPair)0;

    mortonPair.particleIndex = index;
    mortonPair.mortonCode = mortonCode;
    mortonPair.buffer1 = 0;
    mortonPair.buffer2 = 0;
    return mortonPair;
}

MortonCell initMortonCellStruct(uint pairIndex, uint mortonCode)
{
    MortonCell mortonCell = (MortonCell)0;

    mortonCell.mortonCode = mortonCode;
    mortonCell.mortonPairStartIndex = pairIndex;
    mortonCell.mortonPairCount = 0;
    mortonCell.buffer1 = 0;
    return mortonCell;
}
