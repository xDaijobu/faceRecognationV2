using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Android.Content;
using Android.Graphics;
using Android.Util;
using faceRecognationV2.Droid.Utils;
using Java.Lang;
using static faceRecognationV2.Interfaces.ISimilarityClassifier;
using Math = Java.Lang.Math;

namespace faceRecognationV2.Droid.Camera2
{
    public class MultiBoxTracker
    {
        private class TrackedRecognition
        {
            public RectF location;
            public float detectionConfidence;
            public int color;
            public string title;
        }

        private static float TEXT_SIZE_DIP = 18;
        private static float MIN_SIZE = 16.0f;
        private static int[] COLORS =
        {
            Color.Blue,
            Color.Red,
            Color.Green,
            Color.Yellow,
            Color.Cyan,
            Color.Magenta,
            Color.White,
            Color.ParseColor("#55FF55"),
            Color.ParseColor("#FFA500"),
            Color.ParseColor("#FF8888"),
            Color.ParseColor("#AAAAFF"),
            Color.ParseColor("#FFFFAA"),
            Color.ParseColor("55AAAA"),
            Color.ParseColor("#0D0068"),
        };

        private Queue<int> availableColors = new Queue<int>();
        List<(Float, RectF)> screenRects = new List<(Float, RectF)>();
        List<TrackedRecognition> trackedObjects = new List<TrackedRecognition>();
        private Paint boxPaint = new Paint();
        private float textSizePx;
        private BorderedText borderedText;
        private Matrix frameToCanvasMatrix;
        private int frameWidth;
        private int frameHeight;
        private int sensorOrientation;

        public MultiBoxTracker(Context context)
        {
            foreach (var color in COLORS)
                availableColors.Enqueue(color);

            boxPaint.Color = Color.Red;
            boxPaint.SetStyle(Paint.Style.Stroke);
            boxPaint.StrokeWidth = 10.0f;
            boxPaint.StrokeCap = Paint.Cap.Round;
            boxPaint.StrokeJoin = Paint.Join.Round;
            boxPaint.StrokeMiter = 100;

            textSizePx = TypedValue.ApplyDimension(ComplexUnitType.Dip, TEXT_SIZE_DIP, context.Resources.DisplayMetrics);
            borderedText = new BorderedText(textSizePx);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetFrameConfiguration(int width, int height, int sensorOrientation)
        {
            frameWidth = width;
            frameHeight = height;
            this.sensorOrientation = sensorOrientation;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void DrawDebug(Canvas canvas)
        {
            Paint textPaint = new Paint();
            textPaint.Color = Color.White;
            textPaint.TextSize = 60.0f;

            Paint boxPaint = new Paint();
            boxPaint.Color = Color.Red;
            boxPaint.Alpha = 200;
            boxPaint.SetStyle(Paint.Style.Stroke);

            foreach (var detection in screenRects)
            {
                RectF rect = detection.Item2;

                canvas.DrawRect(rect, boxPaint);
                canvas.DrawText("" + detection.Item1, rect.Left, rect.Top, textPaint);
                borderedText.DrawText(canvas, rect.CenterX(), rect.CenterY(), "" + detection.Item1);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void TrackResults(List<Recognition> results, long timestamp)
        {
            System.Diagnostics.Debug.WriteLine("Processing: " + results.Count + " | TimeStamp: " + timestamp);
            ProcessResults(results);
        }

        private Matrix GetFrameToCanvasMatrix()
        {
            return frameToCanvasMatrix;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Draw(Canvas canvas)
        {
            bool rotated = sensorOrientation % 180 == 90;
            float multiplier =
                Math.Min(
                    canvas.Height / (float)(rotated ? frameWidth : frameHeight),
                    canvas.Width / (float)(rotated ? frameHeight : frameWidth));

            frameToCanvasMatrix = ImageUtils.GetTransformationMatrix(
                frameWidth,
                frameHeight,
                (int)(multiplier * (rotated ? frameHeight : frameWidth)),
                (int)(multiplier * (rotated ? frameWidth : frameHeight)),
                sensorOrientation,
                false);


            foreach (var recognition in trackedObjects)
            {
                RectF trackedPos = new RectF(recognition.location);

                GetFrameToCanvasMatrix().MapRect(trackedPos);
                boxPaint.Color = new Color(recognition.color);

                float cornerSize = Math.Min(trackedPos.Width(), trackedPos.Height()) / 8.0f;
                canvas.DrawRoundRect(trackedPos, cornerSize, cornerSize, boxPaint);

                //@SuppressLint("DefaultLocale")
                string strConfidence = recognition.detectionConfidence < 0
                    ? ""
                    : string.Format("%.2f", recognition.detectionConfidence) + "";

                string labelString = !string.IsNullOrEmpty(recognition.title)
                    ? string.Format("%s %s", recognition.title, strConfidence)
                    : strConfidence;

                borderedText.DrawText(canvas, trackedPos.Left + cornerSize, trackedPos.Top, labelString, boxPaint);
            }
        }

        private void ProcessResults(List<Recognition> results)
        {
            List<(float, Recognition)> rectsToTrack = new List<(float, Recognition)>();

            screenRects.Clear();
            Matrix rgbFrameToScreen = new Matrix(GetFrameToCanvasMatrix());

            foreach (var result in results)
            {
                if (result.Location == null)
                    continue;

                RectF detectionFrameRect = new RectF(result.Location);
                RectF detectionScreenRect = new RectF();

                rgbFrameToScreen.MapRect(detectionFrameRect, detectionFrameRect);

                screenRects.Add(((Float, RectF))(result.Distance.Value, detectionScreenRect));

                if (detectionFrameRect.Width() < MIN_SIZE || detectionFrameRect.Height() < MIN_SIZE)
                {
                    System.Diagnostics.Debug.WriteLine("Degenerate rectangle!" + detectionFrameRect);
                    continue;
                }

                rectsToTrack.Add(((float, Recognition))(result.Distance, result));

                trackedObjects.Clear();
                if (rectsToTrack?.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("Nothing to track, aborting.");
                    return;
                }

                foreach (var potential in rectsToTrack)
                {
                    TrackedRecognition trackedRecognition = new TrackedRecognition();
                    trackedRecognition.detectionConfidence = potential.Item1;
                    trackedRecognition.location = new RectF(potential.Item2.Location);
                    trackedRecognition.title = potential.Item2.Title;
                    if (potential.Item2.Color.HasValue)
                        trackedRecognition.color = new Color(potential.Item2.Color.Value);
                    else
                        trackedRecognition.color = COLORS[trackedObjects.Count];

                    trackedObjects.Add(trackedRecognition);

                    if (trackedObjects.Count >= COLORS.Length)
                        break;
                }
            }
        }
    }
}
