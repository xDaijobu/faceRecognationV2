using System.Collections.Generic;
using Android.Graphics;
using static Android.Graphics.Paint;

namespace faceRecognationV2.Droid.Utils
{
    public class BorderedText
    {
        private Paint InteriorPaint;
        private Paint ExteriorPaint;
        public float TextSize;

        public BorderedText(float textSize) : this(Color.White, Color.Black, textSize)
        {
        }

        public BorderedText(int interiorColor, int exteriorColor, float textSize)
        {
            InteriorPaint = new Paint();
            InteriorPaint.TextSize = textSize;
            InteriorPaint.Color = new Color(interiorColor);
            InteriorPaint.SetStyle(Style.Fill);
            InteriorPaint.AntiAlias = false;
            InteriorPaint.Alpha = 255;

            ExteriorPaint = new Paint();
            ExteriorPaint.TextSize = textSize;
            ExteriorPaint.Color = new Color(exteriorColor);
            ExteriorPaint.SetStyle(Style.FillAndStroke);
            ExteriorPaint.StrokeWidth = textSize / 8;
            ExteriorPaint.AntiAlias = false;
            ExteriorPaint.Alpha = 255;

            this.TextSize = textSize;
        }

        public void DrawText(Canvas canvas, float posX, float posY, string text)
        {
            canvas.DrawText(text, posX, posY, ExteriorPaint);
            canvas.DrawText(text, posX, posY, InteriorPaint);
        }

        public void DrawText(Canvas canvas, float posX, float posY, string text, Paint backgroundPaint)
        {
            float width = ExteriorPaint.MeasureText(text);
            float textSize = ExteriorPaint.TextSize;
            Paint paint = new Paint(backgroundPaint);
            paint.SetStyle(Paint.Style.Fill);
            paint.Alpha = 160;

            canvas.DrawRect(posX, posY + (int)textSize, posX + (int)width, posY, paint);

            canvas.DrawText(text, posX, posY + textSize, InteriorPaint);
        }

        public void DrawLines(Canvas canvas, float posX, float posY, List<string> lines)
        {
            int lineNum = 0;
            foreach(var line in lines)
            {
                DrawText(canvas, posX, posY - TextSize * (lines.Count - lineNum - 1), line);
                ++lineNum;
            }
        }

        public void SetTypeface(Typeface typeface)
        {
            InteriorPaint.SetTypeface(typeface);
            ExteriorPaint.SetTypeface(typeface);
        }

        public void SetInteriorColor(int color)
        {
            InteriorPaint.Color = new Color(color);
        }

        public void SetExteriorColor(int color)
        {
            ExteriorPaint.Color = new Color(color);
        }

        public void SetAlpha(int alpha)
        {
            InteriorPaint.Alpha = alpha;
            ExteriorPaint.Alpha = alpha;
        }

        public void GetTextBounds(string line, int index, int count, Rect lineBounds)
        {
            InteriorPaint.GetTextBounds(line, index, count, lineBounds);
        }

        public void SetTextAlign(Align align)
        {
            InteriorPaint.TextAlign = align;
            ExteriorPaint.TextAlign = align;
        }
    }
}