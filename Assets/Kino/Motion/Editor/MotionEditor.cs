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
        SerializedProperty _exposureTime;
        SerializedProperty _sampleCount;
        SerializedProperty _sampleCountValue;
        SerializedProperty _debugMode;

        static int[] _exposureOptions = { 0, 1, 2, 3, 4 };

        static GUIContent[] _exposureOptionLabels = {
            new GUIContent("1 \u2044 15"),
            new GUIContent("1 \u2044 30"),
            new GUIContent("1 \u2044 60"),
            new GUIContent("1 \u2044 125"),
            new GUIContent("Use frame time")
        };

        static GUIContent _textValue = new GUIContent("Value");

        void OnEnable()
        {
            _exposureTime = serializedObject.FindProperty("_exposureTime");
            _sampleCount = serializedObject.FindProperty("_sampleCount");
            _sampleCountValue = serializedObject.FindProperty("_sampleCountValue");
            _debugMode = serializedObject.FindProperty("_debugMode");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.IntPopup(_exposureTime, _exposureOptionLabels, _exposureOptions);
            EditorGUILayout.PropertyField(_sampleCount);

            if (_sampleCount.hasMultipleDifferentValues ||
                _sampleCount.enumValueIndex == (int)Obscurance.SampleCount.Variable)
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
