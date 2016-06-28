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
        #region Public enumerations

        /// How the exposure time is determined.
        public enum ExposureTime {
            /// Use Time.deltaTime as the exposure time.
            DeltaTime,
            /// Use a constant time given to shutterSpeed.
            Constant
        }

        /// Amount of sample points.
        public enum SampleCount {
            /// The minimum amount of samples.
            Low,
            /// A medium amount of samples. Recommended for typical use.
            Medium,
            /// A large amount of samples.
            High,
            /// Use a given number of samples (customSampleCount)
            Custom
        }

        #endregion

        #region Public properties

        /// How the exposure time is determined.
        public ExposureTime exposureTime {
            get { return _exposureTime; }
            set { _exposureTime = value; }
        }

        [SerializeField]
        [Tooltip("How the exposure time is determined.")]
        ExposureTime _exposureTime = ExposureTime.DeltaTime;

        /// The angle of rotary shutter. The larger the angle is, the longer
        /// the exposure time is. This value is only used in delta time mode.
        public float shutterAngle {
            get { return _shutterAngle; }
            set { _shutterAngle = value; }
        }

        [SerializeField, Range(0, 360)]
        [Tooltip("The angle of rotary shutter. Larger values give longer exposure.")]
        float _shutterAngle = 270;

        /// The denominator of the custom shutter speed. This value is only
        /// used in constant time mode.
        public int shutterSpeed {
            get { return _shutterSpeed; }
            set { _shutterSpeed = value; }
        }

        [SerializeField]
        [Tooltip("The denominator of the shutter speed.")]
        int _shutterSpeed = 48;

        /// The amount of sample points, which affects quality and performance.
        public SampleCount sampleCount {
            get { return _sampleCount; }
            set { _sampleCount = value; }
        }

        [SerializeField]
        [Tooltip("The amount of sample points, which affects quality and performance.")]
        SampleCount _sampleCount = SampleCount.Medium;

        /// The number of sample points. This value is only used when
        /// SampleCount.Custom is given to sampleCount.
        public int customSampleCount {
            get { return _customSampleCount; }
            set { _customSampleCount = value; }
        }

        [SerializeField]
        int _customSampleCount = 10;

        /// The maximum length of motion blur, given as a percentage of the
        /// screen height. The larger the value is, the stronger the effects
        /// are, but also the more noticeable artifacts it gets.
        public float maxBlurRadius {
            get { return Mathf.Clamp(_maxBlurRadius, 0.5f, 10.0f); }
            set { _maxBlurRadius = value; }
        }

        [SerializeField, Range(0.5f, 10.0f)]
        [Tooltip("The maximum length of motion blur, given as a percentage " +
         "of the screen height. Larger values may introduce artifacts.")]
        float _maxBlurRadius = 5.0f;

        #endregion

        #region Debug settings

        enum DebugMode { Off, Velocity, NeighborMax, Depth }

        [SerializeField]
        [Tooltip("The debug visualization mode.")]
        DebugMode _debugMode;

        #endregion

        #region Private properties and methods

        [SerializeField] Shader _prefilterShader;
        [SerializeField] Shader _reconstructionShader;

        Material _prefilterMaterial;
        Material _reconstructionMaterial;

        float VelocityScale {
            get {
                if (exposureTime == ExposureTime.Constant)
                    return 1.0f / (shutterSpeed * Time.smoothDeltaTime);
                else // ExposureTime.DeltaTime
                    return Mathf.Clamp01(shutterAngle / 360);
            }
        }

        int LoopCount {
            get {
                switch (_sampleCount)
                {
                    case SampleCount.Low:    return 2;  // 4 samples
                    case SampleCount.Medium: return 5;  // 10 samples
                    case SampleCount.High:   return 10; // 20 samples
                }
                // SampleCount.Custom
                return Mathf.Clamp(_customSampleCount / 2, 1, 64);
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
            var maxBlurPixels = (int)(maxBlurRadius * source.height / 100);

            // Calculate the TileMax size.
            // It should be a multiple of 8 and larger than maxBlur.
            var tileSize = ((maxBlurPixels - 1) / 8 + 1) * 8;

            // Pass 1 - Velocity/depth packing
            _prefilterMaterial.SetFloat("_VelocityScale", VelocityScale);
            _prefilterMaterial.SetFloat("_MaxBlurRadius", maxBlurPixels);

            var vbuffer = GetTemporaryRT(source, 1, packedRTFormat);
            Graphics.Blit(null, vbuffer, _prefilterMaterial, 0);

            // Pass 2 - First TileMax filter (1/4 downsize)
            var tile4 = GetTemporaryRT(source, 4, vectorRTFormat);
            Graphics.Blit(vbuffer, tile4, _prefilterMaterial, 1);

            // Pass 3 - Second TileMax filter (1/2 downsize)
            var tile8 = GetTemporaryRT(source, 8, vectorRTFormat);
            Graphics.Blit(tile4, tile8, _prefilterMaterial, 2);
            ReleaseTemporaryRT(tile4);

            // Pass 4 - Third TileMax filter (reduce to tileSize)
            var tileMaxOffs = Vector2.one * (tileSize / 8.0f - 1) * -0.5f;
            _prefilterMaterial.SetVector("_TileMaxOffs", tileMaxOffs);
            _prefilterMaterial.SetInt("_TileMaxLoop", tileSize / 8);

            var tile = GetTemporaryRT(source, tileSize, vectorRTFormat);
            Graphics.Blit(tile8, tile, _prefilterMaterial, 3);
            ReleaseTemporaryRT(tile8);

            // Pass 5 - NeighborMax filter
            var neighborMax = GetTemporaryRT(source, tileSize, vectorRTFormat);
            Graphics.Blit(tile, neighborMax, _prefilterMaterial, 4);
            ReleaseTemporaryRT(tile);

            // Pass 6 - Reconstruction pass
            _reconstructionMaterial.SetInt("_LoopCount", LoopCount);
            _reconstructionMaterial.SetFloat("_MaxBlurRadius", maxBlurPixels);
            _reconstructionMaterial.SetTexture("_NeighborMaxTex", neighborMax);
            _reconstructionMaterial.SetTexture("_VelocityTex", vbuffer);

            Graphics.Blit(source, destination, _reconstructionMaterial, (int)_debugMode);

            // Cleaning up
            ReleaseTemporaryRT(vbuffer);
            ReleaseTemporaryRT(neighborMax);
        }

        #endregion
    }
}
