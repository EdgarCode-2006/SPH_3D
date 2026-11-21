Shader "Unlit/ParticleUnlitShader"
{
    /*
    * Draws every particle as a camera-facing square (2 triangles, 6 vertices) and turns
    * it into a shaded circle in the fragment shader, so it looks like a sphere.
    * There is no mesh: KernelManager calls Graphics.RenderPrimitives with 6 vertices per
    * instance, and the vertex shader builds the square from SV_VertexID / SV_InstanceID.
    *
    * Opaque with depth writes, so nearer particles correctly hide farther ones whatever
    * order they are drawn in, and there is no blending cost.
    */
    Properties
    {
        // 0 = wall particles are invisible (their squares are collapsed in the vertex shader).
        // Raise it to see the walls faintly, e.g. 0.05. Rendered as a screen-door
        // pattern (a fraction of the pixels), so the walls stay in this opaque pass.
        _BoundaryAlpha ("Wall Particle Alpha", Range(0, 1)) = 0

        // Visual size only: drawn radius = particleRadius * this. Physics is unaffected.
        _SizeScale ("Size Scale", Range(0.1, 2)) = 1
    }
    SubShader
    {
        Tags
        {
            "Queue"="Geometry"
            "RenderType"="Opaque"
        }
        Pass
        {
            ZWrite On
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "../ParticleData.hlsl"

            // Same buffers the simulation uses (see PARTICLE DATA in ComputeMain.compute).
            // positions: xyz = position, w = 1 for wall particles.
            // sortedDensityPressure: x = density (same slot order as positions).
            StructuredBuffer<float4> positions;
            StructuredBuffer<float2> sortedDensityPressure;

            float _BoundaryAlpha;
            float _SizeScale;

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;   // -1..1 across the square, circle = length < 1
                float4 color  : COLOR;
            };

            // Corners of the square as two triangles.
            static const float2 QUAD_CORNERS[6] =
            {
                float2(-1, -1), float2( 1, -1), float2( 1,  1),
                float2(-1, -1), float2( 1,  1), float2(-1,  1)
            };

            // Light direction in view space (+z points at the camera): upper left, front.
            static const float3 LIGHT_DIR = float3(-0.40, 0.55, 0.73);
            static const float AMBIENT = 0.35;

            v2f vert(uint vid : SV_VertexID, uint iid : SV_InstanceID)
            {
                v2f o;
                float4 position = positions[iid];
                bool isWall = position.w != 0.0;
                float density = sortedDensityPressure[iid].x;

                // Hidden walls: put all 6 vertices on one point outside the view, so the
                // triangles have no area and produce no pixels.
                if (isWall && _BoundaryAlpha <= 0.0)
                {
                    o.vertex = float4(2.0, 2.0, 2.0, 1.0);
                    o.uv = float2(0, 0);
                    o.color = float4(0, 0, 0, 0);
                    return o;
                }

                float2 corner = QUAD_CORNERS[vid];
                float radius = particleRadius * _SizeScale;

                // Offset the corner in view space, so the square always faces the camera.
                float3 viewCenter = mul(UNITY_MATRIX_V, float4(position.xyz, 1.0)).xyz;
                float3 viewPos = viewCenter + float3(corner * radius, 0.0);
                o.vertex = mul(UNITY_MATRIX_P, float4(viewPos, 1.0));
                o.uv = corner;

                if (isWall)
                {
                    // Wall particles: plain grey, see-through by the material's slider.
                    o.color = float4(0.7, 0.7, 0.7, _BoundaryAlpha);
                }
                else
                {
                    // 0 = sparse (spray / free surface), 1 = at or above rest density (bulk fluid)
                    float t = saturate((density / restDensity - 0.3) / 0.7);
                    o.color = float4(lerp(float3(1, 1, 1), float3(0.1, 0.4, 1.0), t), 0.5);
                }
                return o;
            }

            // 4x4 ordered-dither threshold in (0, 1) for this pixel.
            float ditherThreshold(float2 pixel)
            {
                static const float BAYER[16] =
                {
                     0,  8,  2, 10,
                    12,  4, 14,  6,
                     3, 11,  1,  9,
                    15,  7, 13,  5
                };
                uint2 p = uint2(pixel) & 3;
                return (BAYER[p.y * 4 + p.x] + 0.5) / 16.0;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // cut the square down to a circle
                float r2 = dot(i.uv, i.uv);
                if (r2 > 1.0)
                    discard;

                // wall particles: keep only a fraction (= alpha) of their pixels
                if (i.color.a < 1.0 && i.color.a <= ditherThreshold(i.vertex.xy))
                    discard;

                // normal of a sphere seen head-on, for simple lighting
                float3 normal = float3(i.uv, sqrt(1.0 - r2));
                float diffuse = saturate(dot(normal, LIGHT_DIR));
                float3 rgb = i.color.rgb * (AMBIENT + (1.0 - AMBIENT) * diffuse);

                return fixed4(rgb, 1.0);
            }
            ENDCG
        }
    }
}
