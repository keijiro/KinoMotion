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
        #region Public nested classes

        /// How the exposure time (shutter speed) is determined.
        public enum ExposureMode {
            /// Constant time exposure (given by exposureTime).
            Constant,
            /// Frame rate dependent exposure. The exposure time is
            /// set to be Time.deltaTime * exposureTimeScale.
            DeltaTime
        }

        /// Amount of sample points.
        public enum SampleCount {
            /// Minimum amount of samples.
            Low,
            /// Medium amount of samples. Recommended for typical use.
            Medium,
            /// Large amount of samples.
            High,
            /// Use a given number of samples (sampleCountValue).
            Variable
        }

        #endregion

        #region Public properties

        /// How the exposure time (shutter speed) is determined.
        public ExposureMode exposureMode {
            get { return _exposureMode; }
            set { _exposureMode = value; }
        }

        [SerializeField]
        [Tooltip("How the exposure time (shutter speed) is determined.")]
        ExposureMode _exposureMode = ExposureMode.DeltaTime;

        /// Denominator of the shutter speed.
        /// This value is only used in the constant exposure mode.
        public int shutterSpeed {
            get { return _shutterSpeed; }
            set { _shutterSpeed = value; }
        }

        [SerializeField]
        [Tooltip("Denominator of the shutter speed.")]
        int _shutterSpeed = 30;

        /// Scale factor to the exposure time.
        /// This value is only used in the delta time exposure mode.
        public float exposureTimeScale {
            get { return _exposureTimeScale; }
            set { _exposureTimeScale = value; }
        }

        [SerializeField]
        [Tooltip("Scale factor to the exposure time.")]
        float _exposureTimeScale = 1;

        /// Amount of sample points, which affects quality and performance.
        public SampleCount sampleCount {
            get { return _sampleCount; }
            set { _sampleCount = value; }
        }

        [SerializeField]
        [Tooltip("Amount of sample points, which affects quality and performance.")]
        SampleCount _sampleCount = SampleCount.Medium;

        /// Determines the number of sample points when SampleCount.Variable
        /// is given to sampleCount. It returns the preset value of the current
        /// setting in other cases.
        public int sampleCountValue {
            get {
                switch (_sampleCount) {
                    case SampleCount.Low:    return 6;
                    case SampleCount.Medium: return 12;
                    case SampleCount.High:   return 24;
                }
                return Mathf.Clamp(_sampleCountValue, 2, 128);
            }
            set { _sampleCountValue = value; }
        }

        [SerializeField]
        int _sampleCountValue = 12;

        /// Determines the maximum length of blur trails, given as a percentage
        /// of the screen height. The larger the value is, the longer the
        /// trails are, but also the more noticeable artifacts it gets.
        public float maxBlurRadius {
            get { return Mathf.Clamp(_maxBlurRadius, 0.5f, 10.0f); }
            set { _maxBlurRadius = value; }
        }

        [SerializeField, Range(0.5f, 10.0f)]
        [Tooltip("Maximum length of blur trails. Specified as a percentage " +
         "of the screen height. Large values may introduce artifacts.")]
        float _maxBlurRadius = 1;

        #endregion

        #region Debug settings

        enum DebugMode { Off, Velocity, NeighborMax, Depth }

        [SerializeField]
        [Tooltip("Debug visualization mode.")]
        DebugMode _debugMode;

        #endregion

        #region Private properties and methods

        [SerializeField] Shader _prefilterShader;
        [SerializeField] Shader _reconstructionShader;

        Material _prefilterMaterial;
        Material _reconstructionMaterial;

        float VelocityScale {
            get {
                if (exposureMode == ExposureMode.Constant)
                    return 1.0f / (shutterSpeed * Time.smoothDeltaTime);
                else // ExposureMode.DeltaTime
                    return exposureTimeScale;
            }
        }

        RenderTexture GetTemporaryRT(Texture source, int divider, RenderTextureFormat format)
        {
            var w = source.width / divider;
            var h = source.height / divider;
            var rt = RenderTexture.GetTemporary(w, h, 0, format);
            rt.filterMode = FilterMode.Point;
            return rt;
        }

        void ReleaseTemporaryRT(RenderTexture rt)
        {
            RenderTexture.ReleaseTemporary(rt);
        }

        #endregion

        #region MonoBehaviour functions

        void OnEnable()
        {
            var prefilterShader = Shader.Find("Hidden/Kino/Motion/Prefilter");
            _prefilterMaterial = new Material(prefilterShader);
            _prefilterMaterial.hideFlags = HideFlags.DontSave;

            var reconstructionShader = Shader.Find("Hidden/Kino/Motion/Reconstruction");
            _reconstructionMaterial = new Material(reconstructionShader);
            _reconstructionMaterial.hideFlags = HideFlags.DontSave;

            GetComponent<Camera>().depthTextureMode |=
                DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
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
            // Texture format for storing packed velocity/depth.
            const RenderTextureFormat packedRTFormat = RenderTextureFormat.ARGB2101010;

            // Texture format for storing 2D vectors.
            const RenderTextureFormat vectorRTFormat = RenderTextureFormat.RGHalf;

            // Calculate the maximum blur radius in pixels.
            var maxBlur = (int)(maxBlurRadius * source.height / 100);

            // Calcurate the size of tiles.
            // It should be a multiple of 8 and larger than maxBlur.
            var tileSize = ((maxBlur - 1) / 8 + 1) * 8;

            // Velocity/depth packing
            _prefilterMaterial.SetFloat("_VelocityScale", VelocityScale);
            _prefilterMaterial.SetFloat("_MaxBlurRadius", maxBlur);

            var vbuffer = GetTemporaryRT(source, 1, packedRTFormat);
            Graphics.Blit(null, vbuffer, _prefilterMaterial, 0);

            // First TileMax filter (1/4 downsize)
            var tile4 = GetTemporaryRT(source, 4, vectorRTFormat);
            Graphics.Blit(vbuffer, tile4, _prefilterMaterial, 1);

            // Second TileMax filter (1/2 downsize)
            var tile8 = GetTemporaryRT(source, 8, vectorRTFormat);
            Graphics.Blit(tile4, tile8, _prefilterMaterial, 2);
            ReleaseTemporaryRT(tile4);

            // Third TileMax filter (reduce to tileSize)
            var tileMaxOffs = Vector2.one * (tileSize / 8.0f - 1) * -0.5f;
            _prefilterMaterial.SetVector("_TileMaxOffs", tileMaxOffs);
            _prefilterMaterial.SetInt("_TileMaxLoop", tileSize / 8);

            var tile = GetTemporaryRT(source, tileSize, vectorRTFormat);
            Graphics.Blit(tile8, tile, _prefilterMaterial, 3);
            ReleaseTemporaryRT(tile8);

            // NeighborMax filter
            var neighborMax = GetTemporaryRT(source, tileSize, vectorRTFormat);
            Graphics.Blit(tile, neighborMax, _prefilterMaterial, 4);
            ReleaseTemporaryRT(tile);

            // Reconstruction pass
            var loopCount = Mathf.Max(sampleCountValue / 2, 1);
            _reconstructionMaterial.SetInt("_LoopCount", loopCount);
            _reconstructionMaterial.SetFloat("_MaxBlurRadius", maxBlur);
            _reconstructionMaterial.SetTexture("_NeighborMaxTex", neighborMax);
            _reconstructionMaterial.SetTexture("_VelocityTex", vbuffer);

            source.filterMode = FilterMode.Point;
            Graphics.Blit(source, destination, _reconstructionMaterial, (int)_debugMode);

            // Cleaning up
            ReleaseTemporaryRT(vbuffer);
            ReleaseTemporaryRT(neighborMax);
        }

        #endregion
    }
}
