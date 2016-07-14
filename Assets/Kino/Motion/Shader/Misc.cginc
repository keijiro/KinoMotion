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

struct CompressorOutput
{
    half4 luma : SV_Target0;
    half4 chroma : SV_Target1;
};

CompressorOutput frag_Compress(v2f_img i)
{
    float sw = _ScreenParams.x;     // Screen width
    float pw = _ScreenParams.z - 1; // Pixel wdith

    // RGB to YCbCr convertion matrix
    const half3 kY  = half3( 0.299   ,  0.587   ,  0.114   );
    const half3 kCB = half3(-0.168736, -0.331264,  0.5     );
    const half3 kCR = half3( 0.5     , -0.418688, -0.081312);

    // 0: even column, 1: odd column
    half odd = frac(i.uv.x * sw * 0.5) > 0.5;

    // Calculate UV for chroma componetns.
    // It's between the even and odd columns.
    float2 uv_c = i.uv.xy;
    uv_c.x = (floor(uv_c.x * sw * 0.5) * 2 + 1) * pw;

    // Sample the source texture.
    half3 rgb_y = tex2D(_MainTex, i.uv).rgb;
    half3 rgb_c = tex2D(_MainTex, uv_c).rgb;

    #if !UNITY_COLORSPACE_GAMMA
    rgb_y = LinearToGammaSpace(rgb_y);
    rgb_c = LinearToGammaSpace(rgb_c);
    #endif

    // Convertion and subsampling
    CompressorOutput o;
    o.luma = dot(kY, rgb_y);
    o.chroma = dot(lerp(kCB, kCR, odd), rgb_c) + 0.5;
    return o;
}

half3 DecodeHistory(float2 uv, sampler2D lumaTex, sampler2D chromaTex, float4 texelSize)
{
    float sw = texelSize.z; // Screen width
    float pw = texelSize.x; // Pixel wdith

    // Calculate UV for Cb. It's on the even columns.
    float2 uv_cb = uv;
    uv_cb.x = (floor(uv_cb.x * sw * 0.5) * 2 + 0.5) * pw;

    // Calculate UV for Cr. It's on the odd columns.
    float2 uv_cr = uv_cb;
    uv_cr.x += pw;

    // Sample Y, Cb and Cr.
    half y = tex2D(lumaTex, uv).r;
    half cb = tex2D(chromaTex, uv_cb).r - 0.5;
    half cr = tex2D(chromaTex, uv_cr).r - 0.5;

    // Convert to RGB.
    half3 rgb = half3(
        y                + 1.402   * cr,
        y - 0.34414 * cb - 0.71414 * cr,
        y + 1.772   * cb
    );

    #if !UNITY_COLORSPACE_GAMMA
    rgb = GammaToLinearSpace(rgb);
    #endif

    return rgb;
}

// Frame blending shader
half4 frag_FrameBlending(v2f_multitex i) : SV_Target
{
    half4 src = tex2D(_MainTex, i.uv0);

    half3 acc = src.rgb;
    half w = 1;

    acc += DecodeHistory(i.uv1, _History1LumaTex, _History1ChromaTex, _History1LumaTex_TexelSize) * _History1Weight;
    w += _History1Weight;

    acc += DecodeHistory(i.uv1, _History2LumaTex, _History2ChromaTex, _History2LumaTex_TexelSize) * _History2Weight;
    w += _History2Weight;

    acc += DecodeHistory(i.uv1, _History3LumaTex, _History3ChromaTex, _History3LumaTex_TexelSize) * _History3Weight;
    w += _History3Weight;

    acc += DecodeHistory(i.uv1, _History4LumaTex, _History4ChromaTex, _History4LumaTex_TexelSize) * _History4Weight;
    w += _History4Weight;

    return half4(acc / w, src.a);
}

// Debug visualization shaders
half4 frag_Velocity(v2f_multitex i) : SV_Target
{
    half2 v = tex2D(_VelocityTex, i.uv1).xy;
    return half4(v, 0.5, 1);
}

half4 frag_NeighborMax(v2f_multitex i) : SV_Target
{
    half2 v = tex2D(_NeighborMaxTex, i.uv1).xy;
    v = (v / _MaxBlurRadius + 1) / 2;
    return half4(v, 0.5, 1);
}

half4 frag_Depth(v2f_multitex i) : SV_Target
{
    half z = frac(tex2D(_VelocityTex, i.uv1).z * 128);
    return half4(z, z, z, 1);
}
