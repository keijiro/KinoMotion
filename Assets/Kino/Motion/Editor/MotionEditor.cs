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
using UnityEditor;

namespace Kino
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(Motion))]
    public class MotionEditor : Editor
    {
        SerializedProperty _exposureMode;
        SerializedProperty _shutterSpeed;
        SerializedProperty _exposureTimeScale;
        SerializedProperty _sampleCount;
        SerializedProperty _sampleCountValue;
        SerializedProperty _debugMode;

        static GUIContent _textScale = new GUIContent("Scale");
        static GUIContent _textValue = new GUIContent("Value");
        static GUIContent _textTime = new GUIContent("Time = 1 /");

        void OnEnable()
        {
            _exposureMode = serializedObject.FindProperty("_exposureMode");
            _shutterSpeed = serializedObject.FindProperty("_shutterSpeed");
            _exposureTimeScale = serializedObject.FindProperty("_exposureTimeScale");
            _sampleCount = serializedObject.FindProperty("_sampleCount");
            _sampleCountValue = serializedObject.FindProperty("_sampleCountValue");
            _debugMode = serializedObject.FindProperty("_debugMode");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_exposureMode);

            if (_exposureMode.hasMultipleDifferentValues ||
                _exposureMode.enumValueIndex == (int)Motion.ExposureMode.Constant)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_shutterSpeed, _textTime);
                EditorGUI.indentLevel--;
            }

            if (_exposureMode.hasMultipleDifferentValues ||
                _exposureMode.enumValueIndex == (int)Motion.ExposureMode.DeltaTime)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_exposureTimeScale, _textScale);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(_sampleCount);

            if (_sampleCount.hasMultipleDifferentValues ||
                _sampleCount.enumValueIndex == (int)Motion.SampleCount.Variable)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_sampleCountValue, _textValue);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(_debugMode);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
