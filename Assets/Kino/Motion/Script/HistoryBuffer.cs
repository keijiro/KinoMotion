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
using UnityEngine;

namespace Kino
{
    public partial class Motion : MonoBehaviour
    {
        // History buffer used for multi frame blending
        class HistoryBuffer
        {
            #region Public methods

            public HistoryBuffer()
            {
                Frame.textureFormat = DetermineTextureFormat();
                _frameList = new Frame[4];
            }

            public void Release()
            {
                foreach (var frame in _frameList) frame.Release();
                _frameList = null;
            }

            public void SetMaterialProperties(Material material, float strength)
            {
                var t = Time.time;
                var f1 = GetFrameRelative(-1);
                var f2 = GetFrameRelative(-2);
                var f3 = GetFrameRelative(-3);
                var f4 = GetFrameRelative(-4);

                material.SetTexture("_History1Tex", f1.texture);
                material.SetTexture("_History2Tex", f2.texture);
                material.SetTexture("_History3Tex", f3.texture);
                material.SetTexture("_History4Tex", f4.texture);

                material.SetFloat("_History1Weight", f1.CalculateWeight(strength, t));
                material.SetFloat("_History2Weight", f2.CalculateWeight(strength, t));
                material.SetFloat("_History3Weight", f3.CalculateWeight(strength, t));
                material.SetFloat("_History4Weight", f4.CalculateWeight(strength, t));
            }

            public void PushFrame(RenderTexture source)
            {
                // Push only when actual update (ignore paused frame).
                var frameCount = Time.frameCount;
                if (frameCount == _lastFrameCount) return;

                // Update the frame record.
                _frameList[frameCount % _frameList.Length].MakeRecord(source);
                _lastFrameCount = frameCount;
            }

            #endregion

            #region Frame record struct

            struct Frame
            {
                public RenderTexture texture;
                public float time;

                static public RenderTextureFormat textureFormat;

                public float CalculateWeight(float strength, float currentTime)
                {
                    var coeff = Mathf.Lerp(80.0f, 10.0f, strength);
                    return Mathf.Exp((time - currentTime) * coeff);
                }

                public void Release()
                {
                    if (texture != null)
                        RenderTexture.ReleaseTemporary(texture);
                    texture = null;
                }

                public void MakeRecord(RenderTexture source)
                {
                    Release();

                    texture = RenderTexture.GetTemporary(
                        source.width / 2, source.height / 2, 0, textureFormat
                    );
                    Graphics.Blit(source, texture);

                    time = Time.time;
                }
            }

            #endregion

            #region Private members

            Frame[] _frameList;
            int _lastFrameCount;

            // Retrieve a frame record with relative indexing.
            // Use a negative index to refer to previous frames.
            Frame GetFrameRelative(int offset)
            {
                var index = (Time.frameCount + _frameList.Length + offset) % _frameList.Length;
                return _frameList[index];
            }

            // Determine the texture format to store frames.
            // Tries to use one of the 16-bit color formats if available.
            static RenderTextureFormat DetermineTextureFormat()
            {
                RenderTextureFormat[] formats = {
                    RenderTextureFormat.RGB565,
                    RenderTextureFormat.ARGB1555,
                    RenderTextureFormat.ARGB4444
                };

                foreach (var f in formats)
                    if (SystemInfo.SupportsRenderTextureFormat(f))
                        return f;

                return RenderTextureFormat.Default;
            }

            #endregion
        }
    }
}
