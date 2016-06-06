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

    // Filter parameters/coefficients
    int _LoopCount;
    float _MaxBlurRadius;

    static const float kDepthFilterCoeff = 15;

    // Safer version of vector normalization function
    float2 SafeNorm(float2 v)
    {
        float l = max(length(v), 1e-6);
        return v / l * (l >= 0.5);
    }

    // Interleaved gradient function from Jimenez 2014 http://goo.gl/eomGso
    float GradientNoise(float2 uv)
    {
        uv = floor((uv + _Time.y) * _ScreenParams.xy);
        float f = dot(float2(0.06711056f, 0.00583715f), uv);
        return frac(52.9829189f * frac(f));
    }

    // Jitter function for tile lookup
    float2 JitterTile(float2 uv)
    {
        float rx, ry;
        sincos(GradientNoise(uv + float2(2, 0)) * UNITY_PI * 2, ry, rx);
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
        return saturate(1.0 - kDepthFilterCoeff * (zb - za) / min(za, zb));
    }

    // Lerp and normalization
    float2 RNMix(float2 a, float2 b, float p)
    {
        return SafeNorm(lerp(a, b, saturate(p)));
    }

    // Velocity sampling function
    float3 SampleVelocity(float2 uv)
    {
        float3 v = tex2D(_VelocityTex, uv).xyz;
        return float3((v.xy * 2 - 1) * _MaxBlurRadius, v.z);
    }

    // Sample weighting function
    float SampleWeight(float2 d_n, float l_v_c, float z_p, float T, float2 S_uv, float w_A)
    {
        float3 temp = tex2Dlod(_VelocityTex, float4(S_uv, 0, 0));

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

    // Vertex shader for multiple texture blitting
    struct v2f_multitex
    {
        float4 pos : SV_POSITION;
        float2 uv0 : TEXCOORD0;
        float2 uv1 : TEXCOORD1;
    };

    v2f_multitex vert_multitex(appdata_full v)
    {
        v2f_multitex o;
        o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
        o.uv0 = v.texcoord.xy;
        o.uv1 = v.texcoord.xy;
    #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0.0)
            o.uv1.y = 1.0 - v.texcoord.y;
    #endif
        return o;
    }

    // Reconstruction fragment shader
    half4 frag_reconstruction(v2f_multitex i) : SV_Target
    {
        float2 p = i.uv1 * _ScreenParams.xy;
        float2 p_uv = i.uv1;

        // Velocity vector at p.
        float3 v_c_t = SampleVelocity(p_uv);
        float2 v_c = v_c_t.xy;
        float2 v_c_n = SafeNorm(v_c);
        float l_v_c = max(length(v_c), 0.5);

        // NeighborMax vector at p (with small).
        float2 v_max = tex2D(_NeighborMaxTex, p_uv + JitterTile(p_uv)).xy;
        float2 v_max_n = SafeNorm(v_max);
        float l_v_max = length(v_max);

        // Escape early if the NeighborMax vector is too short.
        if (l_v_max < 0.5) return tex2D(_MainTex, i.uv0);

        // Linearized depth at p.
        float z_p = v_c_t.z;

        // A vector perpendicular to v_max.
        float2 w_p = v_max_n.yx * float2(-1, 1);
        if (dot(w_p, v_c) < 0.0) w_p = -w_p;

        // Secondary sampling direction.
        float2 w_c = RNMix(w_p, v_c_n, (l_v_c - 0.5) / 1.5);

        // The center sample.
        float sampleCount = _LoopCount * 2.0f;
        float totalWeight = sampleCount / (l_v_c * 40);
        float3 result = tex2D(_MainTex, i.uv0) * totalWeight;

        // Start from t=-1 + small jitter.
        // The width of jitter is equivalent to 4 sample steps.
        float sampleJitter = 4.0 * 2 / (sampleCount + 4);
        float t = -1.0 + GradientNoise(p_uv) * sampleJitter;
        float dt = (2.0 - sampleJitter) / sampleCount;

        // Precalculate the w_A parameters.
        float w_A1 = dot(w_c, v_c_n);
        float w_A2 = dot(w_c, v_max_n);

        UNITY_LOOP for (int c = 0; c < _LoopCount; c++)
        {
            // Odd-numbered sample: sample along v_c.
            {
                float2 S_uv0 = i.uv0 + t * v_c * _MainTex_TexelSize.xy;
                float2 S_uv1 = i.uv1 + t * v_c * _VelocityTex_TexelSize.xy;
                float weight = SampleWeight(v_c_n, l_v_c, z_p, abs(t * l_v_max), S_uv1, w_A1);

                result += tex2Dlod(_MainTex, float4(S_uv0, 0, 0)).rgb * weight;
                totalWeight += weight;

                t += dt;
            }
            // Even-numbered sample: sample along v_max.
            {
                float2 S_uv0 = i.uv0 + t * v_max * _MainTex_TexelSize.xy;
                float2 S_uv1 = i.uv1 + t * v_max * _VelocityTex_TexelSize.xy;
                float weight = SampleWeight(v_max_n, l_v_c, z_p, abs(t * l_v_max), S_uv1, w_A2);

                result += tex2Dlod(_MainTex, float4(S_uv0, 0, 0)).rgb * weight;
                totalWeight += weight;

                t += dt;
            }
        }

        return float4(result / totalWeight, 1);
    }

    // Debug visualization shaders
    half4 frag_velocity(v2f_multitex i) : SV_Target
    {
        half2 v = tex2D(_VelocityTex, i.uv1).xy;
        return half4(v, 0.5, 1);
    }

    half4 frag_neighbormax(v2f_multitex i) : SV_Target
    {
        half2 v = tex2D(_NeighborMaxTex, i.uv1).xy;
        v = (v / _MaxBlurRadius + 1) / 2;
        return half4(v, 0.5, 1);
    }

    half4 frag_depth(v2f_multitex i) : SV_Target
    {
        half z = frac(tex2D(_VelocityTex, i.uv1).z * 128);
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
            #pragma vertex vert_multitex
            #pragma fragment frag_reconstruction
            ENDCG
        }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_multitex
            #pragma fragment frag_velocity
            ENDCG
        }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_multitex
            #pragma fragment frag_neighbormax
            ENDCG
        }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_multitex
            #pragma fragment frag_depth
            ENDCG
        }
    }
}
