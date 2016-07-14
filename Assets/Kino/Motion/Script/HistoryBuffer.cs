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

                material.SetTexture("_History1LumaTex", f1.lumaTexture);
                material.SetTexture("_History2LumaTex", f2.lumaTexture);
                material.SetTexture("_History3LumaTex", f3.lumaTexture);
                material.SetTexture("_History4LumaTex", f4.lumaTexture);

                material.SetTexture("_History1ChromaTex", f1.chromaTexture);
                material.SetTexture("_History2ChromaTex", f2.chromaTexture);
                material.SetTexture("_History3ChromaTex", f3.chromaTexture);
                material.SetTexture("_History4ChromaTex", f4.chromaTexture);

                material.SetFloat("_History1Weight", f1.CalculateWeight(strength, t));
                material.SetFloat("_History2Weight", f2.CalculateWeight(strength, t));
                material.SetFloat("_History3Weight", f3.CalculateWeight(strength, t));
                material.SetFloat("_History4Weight", f4.CalculateWeight(strength, t));
            }

            public void PushFrame(RenderTexture source, Material material)
            {
                // Push only when actual update (ignore paused frame).
                var frameCount = Time.frameCount;
                if (frameCount == _lastFrameCount) return;

                // Update the frame record.
                _frameList[frameCount % _frameList.Length].MakeRecord(source, material);
                _lastFrameCount = frameCount;
            }

            #endregion

            #region Frame record struct

            struct Frame
            {
                public RenderTexture lumaTexture;
                public RenderTexture chromaTexture;
                public float time;

                RenderBuffer[] _mrt;

                public float CalculateWeight(float strength, float currentTime)
                {
                    var coeff = Mathf.Lerp(80.0f, 10.0f, strength);
                    return Mathf.Exp((time - currentTime) * coeff);
                }

                public void Release()
                {
                    if (lumaTexture != null) RenderTexture.ReleaseTemporary(lumaTexture);
                    if (chromaTexture != null) RenderTexture.ReleaseTemporary(chromaTexture);
                    lumaTexture = null;
                    chromaTexture = null;
                }

                public void MakeRecord(RenderTexture source, Material material)
                {
                    Release();

                    lumaTexture = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.R8);
                    chromaTexture = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.R8);

                    lumaTexture.filterMode = FilterMode.Point;
                    chromaTexture.filterMode = FilterMode.Point;

                    if (_mrt == null) _mrt = new RenderBuffer[2];

                    _mrt[0] = lumaTexture.colorBuffer;
                    _mrt[1] = chromaTexture.colorBuffer;

                    Graphics.SetRenderTarget(_mrt, lumaTexture.depthBuffer);
                    Graphics.Blit(source, material, 7);

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

            #endregion
        }
    }
}
