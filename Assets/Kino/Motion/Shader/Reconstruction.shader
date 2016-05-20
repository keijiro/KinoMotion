//
// Kino/Motion - Motion blur effect
//
// Copyright (C) 2016 Keijiro Takahashi
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
Shader "Hidden/Kino/Motion/Reconstruction"
{
    Properties
    {
        _MainTex       ("", 2D) = ""{}
        _VelocityTex   ("", 2D) = ""{}
        _NeighborMaxTex("", 2D) = ""{}
    }

    CGINCLUDE

    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;

    sampler2D _VelocityTex;
    float4 _VelocityTex_TexelSize;

    sampler2D _NeighborMaxTex;
    float4 _NeighborMaxTex_TexelSize;

    // Filter variables
    float _MaxBlurRadius;
    float _DepthFilterStrength;
    int _LoopCount;

    // Filter coefficients
    static const float sample_jitter = 2;

    // Safer version of vector normalization function
    float2 SafeNorm(float2 v)
    {
        float l = max(length(v), 1e-6);
        return v / l * step(0.5, l);
    }

    // Interleaved gradient function from Jimenez 2014 http://goo.gl/eomGso
    float GradientNoise(float2 uv, float2 offs)
    {
        uv = floor(uv * _ScreenParams.xy) + offs;
        float f = dot(float2(0.06711056f, 0.00583715f), uv);
        return frac(52.9829189f * frac(f));
    }

    // Jitter function for tile lookup
    float2 JitterTile(float2 uv)
    {
        float rx, ry;
        sincos(GradientNoise(uv, float2(3, 2)) * UNITY_PI * 2, ry, rx);
        return float2(rx, ry) * _NeighborMaxTex_TexelSize.xy / 4;
    }

    // Cone shaped interpolation
    float Cone(float T, float l_V)
    {
        return saturate(1.0 - T / l_V);
    }

    // Cylinder shaped interpolation
    float Cylinder(float T, float l_V)
    {
        return 1.0 - smoothstep(0.95 * l_V, 1.05 * l_V, T);
    }

    // Depth comparison function
    float CompareDepth(float za, float zb)
    {
        return saturate(1.0 - _DepthFilterStrength * (zb - za) / min(za, zb));
    }

    // Lerp and normalization
    float2 RNMix(float2 a, float2 b, float p)
    {
        return SafeNorm(lerp(a, b, saturate(p)));
    }

    // Velocity sampling function
    float3 SampleVelocity(float2 uv)
    {
        float3 v = tex2D(_VelocityTex, uv);
        return float3((v.xy * 2 - 1) * _MaxBlurRadius, v.z);
    }

    // Sample weighting function
    float SampleWeight(float2 d_n, float l_v_c, float z_p, float T, float2 S_uv, float w_A)
    {
        float3 temp = tex2D(_VelocityTex, S_uv);

        float2 v_S = (temp.xy * 2 - 1) * _MaxBlurRadius;
        float l_v_S = max(length(v_S), 0.5);

        float z_S = temp.z;

        float f = CompareDepth(z_p, z_S);
        float b = CompareDepth(z_S, z_p);

        float w_B = abs(dot(v_S / l_v_S, d_n));

        float weight = 0.0;
        weight += f * Cone(T, l_v_S) * w_B;
        weight += b * Cone(T, l_v_c) * w_A;
        weight += Cylinder(T, min(l_v_S, l_v_c)) * max(w_A, w_B) * 2;

        return weight;
    }

    // Reconstruction filter
    half4 frag_reconstruction(v2f_img i) : SV_Target
    {
        float2 p = i.uv * _ScreenParams.xy;
        float2 p_uv = i.uv;

        // Velocity vector at p.
        float3 v_c_t = SampleVelocity(p_uv);
        float2 v_c = v_c_t.xy;
        float2 v_c_n = SafeNorm(v_c);
        float l_v_c = max(length(v_c), 0.5);

        // Nightbor-max vector at p with small jitter.
        float2 v_max = tex2D(_NeighborMaxTex, p_uv + JitterTile(p_uv)).xy;
        float2 v_max_n = SafeNorm(v_max);
        float l_v_max = length(v_max);

        // Linearized depth at p.
        float z_p = v_c_t.z;

        // A vector perpendicular to v_max.
        float2 w_p = v_max_n.yx * float2(-1, 1);
        if (dot(w_p, v_c) < 0.0) w_p = -w_p;

        // Alternative sampling direction.
        float2 w_c = RNMix(w_p, v_c_n, (l_v_c - 0.5) / 1.5);

        // First itegration sample (center sample).
        float sampleCount = _LoopCount * 2.0f;
        float totalWeight = sampleCount / (l_v_c * 40);
        float3 result = tex2D(_MainTex, p_uv) * totalWeight;

        // Start from t = -1 with small jitter.
        float t = -1.0 + GradientNoise(p_uv, 0) * sample_jitter / (sampleCount + sample_jitter);
        float dt = 2.0 / (sampleCount + sample_jitter);

        // Precalc the w_A parameters.
        float w_A1 = dot(w_c, v_c_n);
        float w_A2 = dot(w_c, v_max_n);

        UNITY_LOOP for (int c = 0; c < _LoopCount; c++)
        {
            // Odd-numbered sample: sample along v_c.
            {
                float2 S_uv = (t * v_c + p) * _MainTex_TexelSize.xy;
                float weight = SampleWeight(v_c_n, l_v_c, z_p, abs(t * l_v_max), S_uv, w_A1);

                result += tex2D(_MainTex, S_uv).rgb * weight;
                totalWeight += weight;

                t += dt;
            }
            // Even-numbered sample: sample along v_max.
            {
                float2 S_uv = (t * v_max + p) * _MainTex_TexelSize.xy;
                float weight = SampleWeight(v_max_n, l_v_c, z_p, abs(t * l_v_max), S_uv, w_A2);

                result += tex2D(_MainTex, S_uv).rgb * weight;
                totalWeight += weight;

                t += dt;
            }
        }

        return float4(result / totalWeight, 1);
    }

    // Debug visualization shaders
    half4 frag_velocity(v2f_img i) : SV_Target
    {
        half2 v = tex2D(_VelocityTex, i.uv).xy;
        return half4(v, 0.5, 1);
    }

    half4 frag_neighbormax(v2f_img i) : SV_Target
    {
        half2 v = tex2D(_NeighborMaxTex, i.uv).xy;
        v = (v / _MaxBlurRadius + 1) / 2;
        return half4(v, 0.5, 1);
    }

    half4 frag_depth(v2f_img i) : SV_Target
    {
        half z = frac(tex2D(_VelocityTex, i.uv).z * 128);
        return half4(z, z, z, 1);
    }

    ENDCG

    Subshader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag_reconstruction
            ENDCG
        }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_velocity
            ENDCG
        }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_neighbormax
            ENDCG
        }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_depth
            ENDCG
        }
    }
}
