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
                if (_exposureMode == ExposureMode.Constant)
                    return 1.0f / (_shutterSpeed * Time.smoothDeltaTime);
                else // ExposureMode.DeltaTime
                    return _exposureTimeScale;
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
            _reconstructionMaterial.SetInt("_LoopCount", Mathf.Max(sampleCountValue / 2, 1));

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
