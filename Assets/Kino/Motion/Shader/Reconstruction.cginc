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
    half rcp_d_p = 1 / vd_p.z;

    // NeighborMax vector at the center point
    half2 v_max = tex2D(_NeighborMaxTex, i.uv1 + JitterTile(i.uv1)).xy;
    half l_v_max = length(v_max);
    half rcp_l_v_max = 1 / l_v_max;

    // Escape early if the NeighborMax is small enough.
    if (l_v_max < 2) return c_p;

    // Use V_p as a secondary sampling direction except when it's too small
    // compared to V_max. The length of this vector should equal to V_max.
    float2 v_alt = (l_v_p * 2 > l_v_max) ? vd_p.xy / l_v_p * l_v_max : v_max;

    // Sampling direction vectors. Packed in [(x, y), normalized(x, y)]
    float4 v1 = float4(v_max, v_max * rcp_l_v_max);
    float4 v2 = float4(v_alt, v_alt * rcp_l_v_max);

    // Determine the sample count.
    half sc = floor(min(_LoopCount, l_v_max / 2));

    // Loop variables
    half dt = 1 / sc;
    half t = 1 - dt / 2;
    half t_offs = (GradientNoise(i.uv0) - 0.5) * dt;
    bool swap = false;

    // BG velocity (used for tracking the maximum velocity in the BG layer)
    float l_v_bg = max(l_v_p, 1);

    // Color accumlation
    half4 acc = 0;

    UNITY_LOOP while (t > dt / 4)
    {
        float4 v_s = v1;
        half t2 = (swap ? -t : t) + t_offs;

        // Distance to this point
        half l_t = l_v_max * abs(t2);

        // UVs for this sample point
        float2 uv0 = i.uv0 + v_s.xy * t2 * _MainTex_TexelSize.xy;
        float2 uv1 = i.uv1 + v_s.xy * t2 * _VelocityTex_TexelSize.xy;

        // Velocity/Depth at this point
        half3 vd = SampleVelocity(uv1);
        half l_v = length(vd.xy);

        // BG/FG separation
        half fg = saturate((vd_p.z - vd.z) * 20 * rcp_d_p);
        half bg = 1 - fg;

        // Update the BG velocity.
        l_v_bg = max(l_v_bg, lerp(min(l_v, l_v_p), l_v, fg));

        // Distance test and sample weighting
        fg *= saturate(l_v - l_t) / l_v;
        bg *= saturate(l_v_bg - l_t) / l_v_bg;

        // Color accumulation
        half4 c = half4(tex2Dlod(_MainTex, float4(uv0, 0, 0)).rgb, 1);
        acc += c * (fg + bg) * (1.2 - t); // triangular window

        // Advance to the next sample.
        t -= dt * swap;
        swap = !swap;
    }

    // Add the center sample.
    acc += half4(c_p.rgb, 1) * 1.2 / (l_v_bg * sc * 2);

    return half4(acc.rgb / acc.a, c_p.a);
}
