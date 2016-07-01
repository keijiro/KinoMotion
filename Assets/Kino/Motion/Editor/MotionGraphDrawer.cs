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
    public class MotionGraphDrawer
    {
        public MotionGraphDrawer(Texture blendingIcon)
        {
            _blendingIcon = blendingIcon;
            _captionStyle = new GUIStyle(EditorStyles.miniLabel);
            _captionStyle.alignment = TextAnchor.LowerCenter;
        }

        public void DrawShutterGraph(float angle)
        {
            var height = 32;

            _rect = GUILayoutUtility.GetRect(128, height);

            DrawDisc(0.2f, 0.5f, height * 0.38f, colorWhite);
            DrawDisc(0.2f, 0.5f, height * 0.18f, colorGray);
            DrawArc(0.2f, 0.5f, height * 0.5f, (360 - angle), colorGray);
            DrawDisc(0.2f, 0.5f, 1, colorWhite);

            DrawRect(0.35f, 0.25f, 0.95f, 0.75f, 0.18f, 0.45f);
            DrawRect(0.35f, 0.25f, Mathf.Lerp(0.35f, 0.95f, angle / 360), 0.75f, 0.45f, 0);

            var labelPos = PointInRect(0.35f, 0.7f) + Vector3.right * 2;
            var labelText = "Exposure time = " + (angle / 3.6f).ToString("0") + "% of ΔT";
            Handles.Label(labelPos, labelText, EditorStyles.miniLabel);
        }

        public void DrawBlendingGraph(float strength)
        {
            var height = 32;

            _rect = GUILayoutUtility.GetRect(128, height);

            var iconSize = new Vector2(height, height);
            var iconTop = _rect.center - Vector2.up * (height / 2);
            var iconWidth = new Vector2(height, 0); 

            var weight1 = BlendingWeight(strength, 4.0f / 60);
            var weight2 = BlendingWeight(strength, 3.0f / 60);
            var weight3 = BlendingWeight(strength, 2.0f / 60);
            var weight4 = BlendingWeight(strength, 1.0f / 60);
            var weight5 = 1.0f;

            var rect1 = new Rect(iconTop + iconWidth * 1.1f, iconSize);
            var rect2 = new Rect(iconTop + iconWidth * 0.3f, iconSize);
            var rect3 = new Rect(iconTop - iconWidth * 0.5f, iconSize);
            var rect4 = new Rect(iconTop - iconWidth * 1.3f, iconSize);
            var rect5 = new Rect(iconTop - iconWidth * 2.1f, iconSize);

            GUI.color = Grayscale(0.4f, weight1); GUI.Label(rect1, _blendingIcon);
            GUI.color = Grayscale(0.4f, weight2); GUI.Label(rect2, _blendingIcon);
            GUI.color = Grayscale(0.4f, weight3); GUI.Label(rect3, _blendingIcon);
            GUI.color = Grayscale(0.4f, weight4); GUI.Label(rect4, _blendingIcon);
            GUI.color = Grayscale(0.4f, weight5); GUI.Label(rect5, _blendingIcon);

            GUI.color = Color.white;
            GUI.Label(rect1, (weight1 * 100).ToString("0") + "%", _captionStyle);
            GUI.Label(rect2, (weight2 * 100).ToString("0") + "%", _captionStyle);
            GUI.Label(rect3, (weight3 * 100).ToString("0") + "%", _captionStyle);
            GUI.Label(rect4, (weight4 * 100).ToString("0") + "%", _captionStyle);
            GUI.Label(rect5, (weight5 * 100).ToString("0") + "%", _captionStyle);
        }

        static Color colorGray = new Color(0.16f, 0.16f, 0.16f);
        static Color colorWhite = new Color(0.45f, 0.45f, 0.45f);

        Rect _rect;
        Vector3[] _rectVertices = new Vector3[4];

        Texture _blendingIcon;
        GUIStyle _captionStyle;

        float BlendingWeight(float strength, float time)
        {
            if (strength > 0)
                return Mathf.Exp(-time * Mathf.Lerp(80.0f, 10.0f, strength));
            else
                return 0;
        }

        // Grayscale color
        Color Grayscale(float level, float alpha = 1)
        {
            return new Color(level, level, level, alpha);
        }

        // Transform a point into the graph rect.
        Vector3 PointInRect(float x, float y)
        {
            x = Mathf.Lerp(_rect.x, _rect.xMax, x);
            y = Mathf.Lerp(_rect.yMax, _rect.y, y);
            return new Vector3(x, y, 0);
        }

        // Draw a solid disc in the graph rect.
        void DrawDisc(float x, float y, float radius, Color fill)
        {
            Handles.color = fill;
            Handles.DrawSolidDisc(PointInRect(x, y), Vector3.forward, radius);
        }

        // Draw an arc in the graph rect.
        void DrawArc(float x, float y, float radius, float angle, Color fill)
        {
            var center = PointInRect(x, y);

            var start = new Vector2(
                -Mathf.Cos(Mathf.Deg2Rad * angle / 2),
                 Mathf.Sin(Mathf.Deg2Rad * angle / 2)
            );

            Handles.color = fill;
            Handles.DrawSolidArc(center, Vector3.forward, start, angle, radius);
        }

        // Draw a rectangle in the graph rect.
        void DrawRect(float x1, float y1, float x2, float y2, float fill, float line)
        {
            _rectVertices[0] = PointInRect(x1, y1);
            _rectVertices[1] = PointInRect(x2, y1);
            _rectVertices[2] = PointInRect(x2, y2);
            _rectVertices[3] = PointInRect(x1, y2);

            Handles.color = Color.white;
            Handles.DrawSolidRectangleWithOutline(
                _rectVertices,
                fill < 0 ? Color.clear : Color.white * fill,
                line < 0 ? Color.clear : Color.white * line
            );
        }
    }
}
