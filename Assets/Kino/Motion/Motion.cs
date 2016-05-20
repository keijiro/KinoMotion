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
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Kino Image Effects/Motion")]
    public class Motion : MonoBehaviour
    {
        #region Public properties

        /// Exposure time (shutter speed) of camera.
        public ExposureTime exposureTime {
            get { return _exposureTime; }
            set { _exposureTime = value; }
        }

        public enum ExposureTime {
            OnePerFifteen,
            OnePerThirty,
            OnePerSixty,
            OnePerOneTwentyFive,
            UseFrameTime
        }

        [SerializeField]
        [Tooltip("Exposure time (shutter speed) of camera.")]
        ExposureTime _exposureTime = ExposureTime.OnePerThirty;

        /// Number of sample points, which affects quality and performance.
        public SampleCount sampleCount {
            get { return _sampleCount; }
            set { _sampleCount = value; }
        }

        public enum SampleCount { Low, Medium, High, Variable }

        [SerializeField]
        [Tooltip("Number of sample points, which affects quality and performance.")]
        SampleCount _sampleCount = SampleCount.Medium;

        /// Determines the sample count when SampleCount.Variable is used.
        /// In other cases, it returns the preset value of the current setting.
        public int sampleCountValue {
            get {
                switch (_sampleCount) {
                    case SampleCount.Low:    return 10;
                    case SampleCount.Medium: return 20;
                    case SampleCount.High:   return 30;
                }
                return Mathf.Clamp(_sampleCountValue, 1, 256);
            }
            set { _sampleCountValue = value; }
        }

        [SerializeField]
        int _sampleCountValue = 16;

        /// Debug visualization mode.
        [SerializeField]
        DebugMode _debugMode;

        public enum DebugMode { Off, Velocity, NeighborMax, Depth }

        #endregion

        #region Private properties and methods

        [SerializeField] Shader _prefilterShader;
        [SerializeField] Shader _reconstructionShader;

        Material _prefilterMaterial;
        Material _reconstructionMaterial;

        static float[] exposureTimeTable = { 15, 30, 60, 125, 1 };

        float VelocityScale {
            get {
                if (_exposureTime == ExposureTime.UseFrameTime) return 1;
                var time = exposureTimeTable[(int)_exposureTime];
                return 1 / (time * Time.smoothDeltaTime);
            }
        }

        #endregion

        #region MonoBehaviour functions

        void OnEnable()
        {
            _prefilterMaterial = new Material(Shader.Find("Hidden/Kino/Motion/Prefilter"));
            _prefilterMaterial.hideFlags = HideFlags.DontSave;

            _reconstructionMaterial = new Material(Shader.Find("Hidden/Kino/Motion/Reconstruction"));
            _reconstructionMaterial.hideFlags = HideFlags.DontSave;

            GetComponent<Camera>().depthTextureMode |= DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
        }

        void OnDisable()
        {
            DestroyImmediate(_prefilterMaterial);
            _prefilterMaterial = null;

            DestroyImmediate(_reconstructionMaterial);
            _reconstructionMaterial = null;
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            var tw = source.width;
            var th = source.height;

            _prefilterMaterial.SetFloat("_VelocityScale", VelocityScale);
            _prefilterMaterial.SetFloat("_MaxBlurRadius", 40);

            _reconstructionMaterial.SetFloat("_MaxBlurRadius", 40);
            _reconstructionMaterial.SetFloat("_DepthFilterStrength", 5);
            _reconstructionMaterial.SetInt("_LoopCount", sampleCountValue / 2);

            var vbuffer = RenderTexture.GetTemporary(tw, th, 0, RenderTextureFormat.ARGB2101010);
            var tile1 = RenderTexture.GetTemporary(tw / 10, th / 10, 0, RenderTextureFormat.RGHalf);
            var tile2 = RenderTexture.GetTemporary(tw / 40, th / 40, 0, RenderTextureFormat.RGHalf);
            var tile3 = RenderTexture.GetTemporary(tw / 40, th / 40, 0, RenderTextureFormat.RGHalf);

            source.filterMode = FilterMode.Point;
            vbuffer.filterMode = FilterMode.Point;
            tile1.filterMode = FilterMode.Point;
            tile2.filterMode = FilterMode.Point;
            tile3.filterMode = FilterMode.Point;

            Graphics.Blit(source, vbuffer, _prefilterMaterial, 0);
            Graphics.Blit(vbuffer, tile1, _prefilterMaterial, 1);
            Graphics.Blit(tile1, tile2, _prefilterMaterial, 2);
            Graphics.Blit(tile2, tile3, _prefilterMaterial, 4);

            _reconstructionMaterial.SetTexture("_VelocityTex", vbuffer);
            _reconstructionMaterial.SetTexture("_NeighborMaxTex", tile3);
            Graphics.Blit(source, destination, _reconstructionMaterial, (int)_debugMode);

            RenderTexture.ReleaseTemporary(vbuffer);
            RenderTexture.ReleaseTemporary(tile1);
            RenderTexture.ReleaseTemporary(tile2);
            RenderTexture.ReleaseTemporary(tile3);
        }

        #endregion
    }
}
