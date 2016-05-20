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
Shader "Hidden/Kino/Motion/Prefilter"
{
    Properties
    {
        _MainTex("", 2D) = ""{}
    }

    CGINCLUDE

    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;

    sampler2D_float _CameraDepthTexture;
    sampler2D_half _CameraMotionVectorsTexture;

    // Velocity scale factor used for adjusting exposure time
    float _VelocityScale;

    // Maximum blur radius
    float _MaxBlurRadius;

    // Largest magnitude vector function
    half2 vmax(half2 v1, half2 v2)
    {
        return lerp(v1, v2, dot(v1, v1) < dot(v2, v2));
    }

    // Velocity map setup
    half4 frag_velocity_map(v2f_img i) : SV_Target
    {
        // Sample the motion vector.
        float2 v = tex2D(_CameraMotionVectorsTexture, i.uv).rg;

        // Apply the exposure time.
        v *= _VelocityScale;

        // Halve the velocity and convert to the one-unit-per-pixel scale.
        v = v * 0.5 / _MainTex_TexelSize.xy;

        // Clamp the vector with the maximum blur radius.
        float lv = length(v);
        v *= min(lv, _MaxBlurRadius) / max(lv, 1e-6);

        // Sample the depth of the pixel.
        float d = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv.xy);
        half z01 = Linear01Depth(d);

        // Pack into 10/10/10/2 format.
        return half4((v / _MaxBlurRadius + 1) / 2, z01, 0);
    }

    // TileMax filters
    half4 frag_tile_max_x10(v2f_img i) : SV_Target
    {
        float2 uv = i.uv - _MainTex_TexelSize.xy * 4.5;

        float2 du = float2(_MainTex_TexelSize.x, 0);
        float2 dv = float2(0, _MainTex_TexelSize.y);

        half2 v = 0;

        for (int ix = 0; ix < 10; ix++)
        {
            float2 uv2 = uv;
            for (int iy = 0; iy < 10; iy++)
            {
                v = vmax(v, (tex2D(_MainTex, uv2).rg * 2 - 1) * _MaxBlurRadius);
                uv2 += du;
            }
            uv += dv;
        }

        return half4(v, 0, 0);
    }

    half4 frag_tile_max_x4(v2f_img i) : SV_Target
    {
        float2 tx = _MainTex_TexelSize.xy;

        half2 v01 = tex2D(_MainTex, i.uv + tx * float2(-1.5, -1.5)).rg;
        half2 v02 = tex2D(_MainTex, i.uv + tx * float2(-0.5, -1.5)).rg;
        half2 v03 = tex2D(_MainTex, i.uv + tx * float2(+0.5, -1.5)).rg;
        half2 v04 = tex2D(_MainTex, i.uv + tx * float2(+1.5, -1.5)).rg;

        half2 v05 = tex2D(_MainTex, i.uv + tx * float2(-1.5, -0.5)).rg;
        half2 v06 = tex2D(_MainTex, i.uv + tx * float2(-0.5, -0.5)).rg;
        half2 v07 = tex2D(_MainTex, i.uv + tx * float2(+0.5, -0.5)).rg;
        half2 v08 = tex2D(_MainTex, i.uv + tx * float2(+1.5, -0.5)).rg;

        half2 v09 = tex2D(_MainTex, i.uv + tx * float2(-1.5, +0.5)).rg;
        half2 v10 = tex2D(_MainTex, i.uv + tx * float2(-0.5, +0.5)).rg;
        half2 v11 = tex2D(_MainTex, i.uv + tx * float2(+0.5, +0.5)).rg;
        half2 v12 = tex2D(_MainTex, i.uv + tx * float2(+1.5, +0.5)).rg;

        half2 v13 = tex2D(_MainTex, i.uv + tx * float2(-1.5, +1.5)).rg;
        half2 v14 = tex2D(_MainTex, i.uv + tx * float2(-0.5, +1.5)).rg;
        half2 v15 = tex2D(_MainTex, i.uv + tx * float2(+0.5, +1.5)).rg;
        half2 v16 = tex2D(_MainTex, i.uv + tx * float2(+1.5, +1.5)).rg;

        half2 va = vmax(vmax(vmax(v01, v02), v03), v04);
        half2 vb = vmax(vmax(vmax(v05, v06), v07), v08);
        half2 vc = vmax(vmax(vmax(v09, v10), v11), v12);
        half2 vd = vmax(vmax(vmax(v13, v14), v15), v16);

        return half4(vmax(vmax(vmax(va, vb), vc), vd), 0, 0);
    }

    half4 frag_tile_max_x2(v2f_img i) : SV_Target
    {
        float2 tx = _MainTex_TexelSize.xy;

        half2 v1 = tex2D(_MainTex, i.uv + tx * float2(-0.5, -0.5)).rg;
        half2 v2 = tex2D(_MainTex, i.uv + tx * float2(+0.5, -0.5)).rg;

        half2 v3 = tex2D(_MainTex, i.uv + tx * float2(-0.5, +0.5)).rg;
        half2 v4 = tex2D(_MainTex, i.uv + tx * float2(+0.5, +0.5)).rg;

        return half4(vmax(vmax(vmax(v1, v2), v3), v4), 0, 0);
    }

    // NeighborMax filter
    half4 frag_neighbor_max(v2f_img i) : SV_Target
    {
        static const half cw = 1.01f; // center weight tweak

        float2 tx = _MainTex_TexelSize.xy;

        half2 v1 = tex2D(_MainTex, i.uv + tx * float2(-1, -1)).rg;
        half2 v2 = tex2D(_MainTex, i.uv + tx * float2( 0, -1)).rg;
        half2 v3 = tex2D(_MainTex, i.uv + tx * float2(+1, -1)).rg;

        half2 v4 = tex2D(_MainTex, i.uv + tx * float2(-1,  0)).rg;
        half2 v5 = tex2D(_MainTex, i.uv + tx * float2( 0,  0)).rg * cw;
        half2 v6 = tex2D(_MainTex, i.uv + tx * float2(+1,  0)).rg;

        half2 v7 = tex2D(_MainTex, i.uv + tx * float2(-1, +1)).rg;
        half2 v8 = tex2D(_MainTex, i.uv + tx * float2( 0, +1)).rg;
        half2 v9 = tex2D(_MainTex, i.uv + tx * float2(+1, +1)).rg;

        half2 va = vmax(v1, vmax(v2, v3));
        half2 vb = vmax(v4, vmax(v5, v6));
        half2 vc = vmax(v7, vmax(v8, v9));

        return half4(vmax(va, vmax(vb, vc)) / cw, 0, 0);
    }

    ENDCG

    Subshader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_velocity_map
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_tile_max_x10
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_tile_max_x4
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_tile_max_x2
            #pragma target 3.0
            ENDCG
        }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag_neighbor_max
            #pragma target 3.0
            ENDCG
        }
    }
}
