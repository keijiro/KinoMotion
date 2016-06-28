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

// Debug items are hidden by default (not useful for users).
// #define SHOW_DEBUG

using UnityEngine;
using UnityEditor;

namespace Kino
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(Motion))]
    public class MotionEditor : Editor
    {
        SerializedProperty _exposureTime;
        SerializedProperty _shutterAngle;
        SerializedProperty _shutterSpeed;
        SerializedProperty _sampleCount;
        SerializedProperty _customSampleCount;
        SerializedProperty _maxBlurRadius;
        #if SHOW_DEBUG
        SerializedProperty _debugMode;
        #endif

        static GUIContent _textTime = new GUIContent("Time = 1 /");
        static GUIContent _textCustomValue = new GUIContent("Custom Value");
        static GUIContent _textMaxBlur = new GUIContent("Max Blur Radius %");

        void OnEnable()
        {
            _exposureTime = serializedObject.FindProperty("_exposureTime");
            _shutterAngle = serializedObject.FindProperty("_shutterAngle");
            _shutterSpeed = serializedObject.FindProperty("_shutterSpeed");
            _sampleCount = serializedObject.FindProperty("_sampleCount");
            _customSampleCount = serializedObject.FindProperty("_customSampleCount");
            _maxBlurRadius = serializedObject.FindProperty("_maxBlurRadius");
            #if SHOW_DEBUG
            _debugMode = serializedObject.FindProperty("_debugMode");
            #endif
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Exposure time options
            EditorGUILayout.PropertyField(_exposureTime);

            var showAllItems = _exposureTime.hasMultipleDifferentValues;
            var exposureTime = (Motion.ExposureTime)_exposureTime.enumValueIndex;

            EditorGUI.indentLevel++;

            if (showAllItems || exposureTime == Motion.ExposureTime.FrameRateDependent)
                EditorGUILayout.PropertyField(_shutterAngle);

            if (showAllItems || exposureTime == Motion.ExposureTime.Constant)
                EditorGUILayout.PropertyField(_shutterSpeed, _textTime);

            EditorGUI.indentLevel--;

            // Sample count options
            EditorGUILayout.PropertyField(_sampleCount);

            showAllItems = _sampleCount.hasMultipleDifferentValues;
            var sampleCount = (Motion.SampleCount)_sampleCount.enumValueIndex;

            EditorGUI.indentLevel++;

            if (showAllItems || sampleCount == Motion.SampleCount.Custom)
                EditorGUILayout.PropertyField(_customSampleCount, _textCustomValue);

            EditorGUI.indentLevel--;

            // Other options
            EditorGUILayout.PropertyField(_maxBlurRadius, _textMaxBlur);
            #if SHOW_DEBUG
            EditorGUILayout.PropertyField(_debugMode);
            #endif

            serializedObject.ApplyModifiedProperties();
        }
    }
}
