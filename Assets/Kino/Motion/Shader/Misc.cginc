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

half4 frag_FrameBlending(v2f_multitex i) : SV_Target
{
    half4 last = tex2D(_MainTex, i.uv0);

    half3 sum = last;
    sum += tex2D(_History1Tex, i.uv1) * _History1Weight;
    sum += tex2D(_History2Tex, i.uv1) * _History2Weight;
    sum += tex2D(_History3Tex, i.uv1) * _History3Weight;
    sum += tex2D(_History4Tex, i.uv1) * _History4Weight;

    half total =
        1 + _History1Weight + _History2Weight + _History3Weight + _History4Weight;

    return half4(sum / total, last.a);
}
