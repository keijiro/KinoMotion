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

#include "Common.cginc"

// Strength of the depth filter
static const float kDepthFilterCoeff = 15;

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

// Depth comparison function
half CompareDepth(half za, half zb)
{
    return saturate(1.0 - kDepthFilterCoeff * (zb - za) / min(za, zb));
}

// Velocity sampling function
half3 SampleVelocity(float2 uv)
{
    half3 v = tex2Dlod(_VelocityTex, float4(uv, 0, 0)).xyz;
    return half3((v.xy * 2 - 1) * _MaxBlurRadius, v.z);
}

// Reconstruction fragment shader
half4 frag_Reconstruction(v2f_multitex i) : SV_Target
{
    // Original source color
    half4 c_p = tex2D(_MainTex, i.uv0);

    // Velocity/Depth at the center point
    half3 vd_p = SampleVelocity(i.uv1);
    half l_v_p = max(length(vd_p.xy), 0.5);
    half rcp_l_v_p = 1 / max(1, l_v_p);

    // NeighborMax vector at the center point
    half2 v_max = tex2D(_NeighborMaxTex, i.uv1 + JitterTile(i.uv1)).xy;
    half l_v_max = length(v_max);

    // Escape early if the NeighborMax is small enough.
    if (l_v_max < 1) return c_p;

    // Determine the sample count.
    int sc = min(_LoopCount, l_v_max);

    // Loop variables
    half dt = 2.0 / sc;
    half t = -1.0 + GradientNoise(i.uv0) * dt;

    // Start accumulation.
    // center weight = 1 / (sample_count * max(1, |V_p|))
    half4 acc = half4(c_p.rgb, 1) * 0.5 * dt * rcp_l_v_p;

    UNITY_LOOP for (int lp = 0; lp < sc; lp++)
    {
        // UVs for this sample point
        float2 uv0 = i.uv0 + v_max * t * _MainTex_TexelSize.xy;
        float2 uv1 = i.uv1 + v_max * t * _VelocityTex_TexelSize.xy;

        // Velocity/Depth at this point
        half3 vd = SampleVelocity(uv1);
        half l_v = length(vd.xy);

        // Distance to this point
        half l_t = abs(l_v_max * t);

        // Calculate the sample weight.
        half w1 = (l_v   > l_t) * CompareDepth(vd_p.z, vd.z) / max(1, l_v);
        half w2 = (l_v_p > l_t) * CompareDepth(vd.z, vd_p.z) * rcp_l_v_p;

        // Color accumulation
        half3 c = tex2Dlod(_MainTex, float4(uv0, 0, 0)).rgb;
        acc += half4(c, 1) * max(w1, w2);

        // Advance to the next sample.
        t += dt;
    }

    return half4(acc.rgb / acc.a, c_p.a);
}
