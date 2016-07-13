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
    public partial class Motion : MonoBehaviour
    {
        #region Public properties

        /// The angle of rotary shutter. The larger the angle is, the longer
        /// the exposure time is. This value is only used in delta time mode.
        public float shutterAngle {
            get { return _shutterAngle; }
            set { _shutterAngle = value; }
        }

        [SerializeField, Range(0, 360)]
        [Tooltip("The angle of rotary shutter. Larger values give longer exposure.")]
        float _shutterAngle = 270;

        /// The amount of sample points, which affects quality and performance.
        public int sampleCount {
            get { return _sampleCount; }
            set { _sampleCount = value; }
        }

        [SerializeField]
        [Tooltip("The amount of sample points, which affects quality and performance.")]
        int _sampleCount = 8;

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

        /// The strength of multi frame blending. The opacity of preceding
        /// frames are determined from this coefficient and time differences.
        public float frameBlending {
            get { return _frameBlending; }
            set { _frameBlending = value; }
        }

        [SerializeField, Range(0, 1)]
        [Tooltip("The strength of multi frame blending")]
        float _frameBlending = 0;

        #endregion

        #region Debug settings

        enum DebugMode { Off, Velocity, NeighborMax, Depth }

        [SerializeField]
        [Tooltip("The debug visualization mode.")]
        DebugMode _debugMode;

        #endregion

        #region Private properties and methods

        [SerializeField] Shader _shader;

        Material _material;
        HistoryBuffer _historyBuffer;

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
            _material = new Material(Shader.Find("Hidden/Kino/Motion"));
            _material.hideFlags = HideFlags.DontSave;

            _historyBuffer = new HistoryBuffer();

            GetComponent<Camera>().depthTextureMode |=
                DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
        }

        void OnDisable()
        {
            DestroyImmediate(_material);
            _material = null;

            _historyBuffer.Release();
            _historyBuffer = null;
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
            // Motion vectors are scaled by an empirical factor of 1.45.
            var velocityScale = _shutterAngle / 360 * 1.45f;
            _material.SetFloat("_VelocityScale", velocityScale);
            _material.SetFloat("_MaxBlurRadius", maxBlurPixels);

            var vbuffer = GetTemporaryRT(source, 1, packedRTFormat);
            Graphics.Blit(null, vbuffer, _material, 0);

            // Pass 2 - First TileMax filter (1/4 downsize)
            var tile4 = GetTemporaryRT(source, 4, vectorRTFormat);
            Graphics.Blit(vbuffer, tile4, _material, 1);

            // Pass 3 - Second TileMax filter (1/2 downsize)
            var tile8 = GetTemporaryRT(source, 8, vectorRTFormat);
            Graphics.Blit(tile4, tile8, _material, 2);
            ReleaseTemporaryRT(tile4);

            // Pass 4 - Third TileMax filter (reduce to tileSize)
            var tileMaxOffs = Vector2.one * (tileSize / 8.0f - 1) * -0.5f;
            _material.SetVector("_TileMaxOffs", tileMaxOffs);
            _material.SetInt("_TileMaxLoop", tileSize / 8);

            var tile = GetTemporaryRT(source, tileSize, vectorRTFormat);
            Graphics.Blit(tile8, tile, _material, 3);
            ReleaseTemporaryRT(tile8);

            // Pass 5 - NeighborMax filter
            var neighborMax = GetTemporaryRT(source, tileSize, vectorRTFormat);
            Graphics.Blit(tile, neighborMax, _material, 4);
            ReleaseTemporaryRT(tile);

            // Pass 6 - Reconstruction pass
            _material.SetInt("_LoopCount", Mathf.Clamp(_sampleCount / 2, 1, 64));
            _material.SetFloat("_MaxBlurRadius", maxBlurPixels);
            _material.SetTexture("_NeighborMaxTex", neighborMax);
            _material.SetTexture("_VelocityTex", vbuffer);

            if (_debugMode != DebugMode.Off)
            {
                // Blit with the debug shader.
                Graphics.Blit(source, destination, _material, 6 + (int)_debugMode);
            }
            else if (_frameBlending > 0)
            {
                var temp = GetTemporaryRT(source, 1, source.format);
                Graphics.Blit(source, temp, _material, 5);

                // Pass 7 - Frame blending
                _historyBuffer.SetMaterialProperties(_material, _frameBlending);
                Graphics.Blit(temp, destination, _material, 6);

                // Update frame history
                _historyBuffer.PushFrame(temp);
                ReleaseTemporaryRT(temp);
            }
            else
            {
                // No frame blending: Directory output to the destination.
                Graphics.Blit(source, destination, _material, 5);
            }

            // Cleaning up
            ReleaseTemporaryRT(vbuffer);
            ReleaseTemporaryRT(neighborMax);
        }

        #endregion
    }
}
