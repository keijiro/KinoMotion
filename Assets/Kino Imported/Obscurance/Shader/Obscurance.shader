//
// Kino/Obscurance - SSAO (screen-space ambient obscurance) effect for Unity
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
Shader "Hidden/Kino/Obscurance"
{
    Properties
    {
        _MainTex("", 2D) = ""{}
        _ObscuranceTexture("", 2D) = ""{}
    }
    SubShader
    {
        // 0: Occlusion estimation with CameraDepthTexture
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #define SOURCE_DEPTH 1
            #include "Obscurance.cginc"
            #pragma vertex vert_img
            #pragma fragment frag_ao
            #pragma target 3.0
            ENDCG
        }
        // 1: Occlusion estimation with CameraDepthNormalsTexture
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #define SOURCE_DEPTHNORMALS 1
            #include "Obscurance.cginc"
            #pragma vertex vert_img
            #pragma fragment frag_ao
            #pragma target 3.0
            ENDCG
        }
        // 2: Occlusion estimation with G-Buffer
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #define SOURCE_GBUFFER 1
            #include "Obscurance.cginc"
            #pragma vertex vert_img
            #pragma fragment frag_ao
            #pragma target 3.0
            ENDCG
        }
        // 3: Noise reduction (first pass) with CameraDepthNormalsTexture
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #define SOURCE_DEPTHNORMALS 1
            #include "Obscurance.cginc"
            #pragma vertex vert_img
            #pragma fragment frag_blur1
            #pragma target 3.0
            ENDCG
        }
        // 4: Noise reduction (first pass) with G Buffer
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #define SOURCE_GBUFFER 1
            #include "Obscurance.cginc"
            #pragma vertex vert_img
            #pragma fragment frag_blur1
            #pragma target 3.0
            ENDCG
        }
        // 5: Noise reduction (second pass) with CameraDepthNormalsTexture
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #define SOURCE_DEPTHNORMALS 1
            #include "Obscurance.cginc"
            #pragma vertex vert_img
            #pragma fragment frag_blur2
            #pragma target 3.0
            ENDCG
        }
        // 6: Noise reduction (second pass) with G Buffer
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #define SOURCE_GBUFFER 1
            #include "Obscurance.cginc"
            #pragma vertex vert_img
            #pragma fragment frag_blur2
            #pragma target 3.0
            ENDCG
        }
        // 7: Occlusion combiner
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #include "Obscurance.cginc"
            #pragma vertex vert_multitex
            #pragma fragment frag_combine
            #pragma target 3.0
            ENDCG
        }
        // 8: Occlusion combiner for the ambient-only mode
        Pass
        {
            Blend Zero OneMinusSrcColor, Zero OneMinusSrcAlpha
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #include "Obscurance.cginc"
            #pragma vertex vert_gbuffer
            #pragma fragment frag_gbuffer_combine
            #pragma target 3.0
            ENDCG
        }
    }
}
