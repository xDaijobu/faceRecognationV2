using System;
using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2.Params;
using Android.Util;
using Android.Views;

namespace faceRecognationV2.Droid.CustomViews
{
    public class FaceBoundsView : View
    {
        public FaceBoundsView(Context context, IAttributeSet attrs) :
            base(context, attrs)
        {
            Initialize();
        }

        public FaceBoundsView(Context context, IAttributeSet attrs, int defStyle) :
            base(context, attrs, defStyle)
        {
            Initialize();
        }

        private Paint _paintRect;
        private Paint _paintText;

        private void Initialize()
        {
            _paintRect = new Paint();
            _paintText = new Paint();

            _paintRect.Color = Color.Argb(255, 255, 0, 255);
            _paintRect.StrokeWidth = 3;
            _paintRect.AntiAlias = true;
            _paintRect.SetStyle(Paint.Style.Stroke);

            _paintText.Color = Color.Argb(255, 255, 0, 255);
            _paintText.StrokeWidth = 3;
            _paintText.AntiAlias = true;
            _paintText.TextSize = 50;
            _paintText.SetStyle(Paint.Style.FillAndStroke);
        }

        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);


            if (_faces?.Length > 0)
            {
                foreach (var face in _faces)
                {
                    Rect rect = null;
                    if (_sensorOrientation == 90)
                        rect = new Rect((int)((_previewImageHeight - face.Bounds.Top) * _heightRatio),
                                        (int)(face.Bounds.Left * _widthRatio),
                                        (int)((_previewImageHeight - face.Bounds.Bottom) * _heightRatio),
                                        (int)(face.Bounds.Right * _widthRatio));
                    else if (_sensorOrientation == 270)
                        rect = new Rect((int)((_previewImageHeight - face.Bounds.Top) * _heightRatio),
                                        (int)((_previewImageWidth - face.Bounds.Left) * _widthRatio),
                                        (int)((_previewImageHeight - face.Bounds.Bottom) * _heightRatio),
                                        (int)((_previewImageWidth - face.Bounds.Right) * _widthRatio));

                    canvas.DrawRect(rect, _paintRect);

                    canvas.DrawText("Hai hai hai", rect.Right, rect.Bottom, _paintText);
                }
            }
            else
            {
                canvas.DrawColor(Color.Transparent, PorterDuff.Mode.Clear);
            }
        }


        /// <summary>
        /// tampung semua Faces (RectF to Rect)
        /// olah lgi semua Faces (berdsrkan dgn data yg sdh ada)
        /// baru di draw ?
        /// </summary>

        private Android.Hardware.Camera2.Params.Face[] _faces;

        private double _heightRatio;
        private double _widthRatio;
        private int _previewImageHeight;
        private int _previewImageWidth;
        private int _sensorOrientation;

        public void ShowBoundsOnFace(Android.Hardware.Camera2.Params.Face[] faces,
                                     int textureWidth,
                                     int textureHeight,
                                     int previewImageWidth,
                                     int previewImageHeight,
                                     int sensorOrientation)
        {
            _faces = faces;
            _previewImageHeight = previewImageHeight;
            _previewImageWidth = previewImageWidth;
            _sensorOrientation = sensorOrientation;

            _widthRatio = (double)textureHeight / previewImageWidth;
            _heightRatio = (double)textureWidth / previewImageHeight;

            Invalidate();
        }


        public void OnFacesDetected(long currTimestamp, Face[] faces, bool add)
        {

        }
    }
}
