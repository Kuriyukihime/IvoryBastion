using System;

namespace IvoryBastion
{
    // Canvas-space geometry, kept independent of Unity for offline layout checks.
    internal static class HudPlacement
    {
        internal const float CombatWidth=336,CombatHeight=132,FactoryWidth=224,FactoryHeight=94;
        internal const float FactoryScale=.72f,ModuleGap=12;
        internal struct Box
        {
            internal float Left, Bottom, Right, Top;
            internal Box(float left, float bottom, float right, float top)
            { Left = left; Bottom = bottom; Right = right; Top = top; }
        }
        internal static Box Bounds(float x, float y, float width, float height, float degrees)
        {
            double angle = degrees * Math.PI / 180d;
            float w = (float)(Math.Abs(Math.Cos(angle)) * width + Math.Abs(Math.Sin(angle)) * height) * .5f;
            float h = (float)(Math.Abs(Math.Sin(angle)) * width + Math.Abs(Math.Cos(angle)) * height) * .5f;
            return new Box(x - w, y - h, x + w, y + h);
        }
        internal static void AboveSkills(Box viewport, Box skills, float width, float height, float angle, out float x, out float y)
        {
            var bounds = Bounds(0f, 0f, width, height, angle);
            float halfWidth = bounds.Right, halfHeight = bounds.Top;
            const float gap = 18f, margin = 12f;
            x = skills.Right - halfWidth;
            y = skills.Top + gap + halfHeight;
            if (y + halfHeight > viewport.Top - margin)
            {
                // On short canvases, use free space beside the native skill cluster.
                x = skills.Left - gap - halfWidth;
                y = skills.Bottom + halfHeight;
            }
            x = Math.Max(viewport.Left + margin + halfWidth, Math.Min(viewport.Right - margin - halfWidth, x));
            y = Math.Max(viewport.Bottom + margin + halfHeight, Math.Min(viewport.Top - margin - halfHeight, y));
        }
    }
}
