using System;
using Xamarin.Forms;

namespace faceRecognationV2.Controls
{
    public enum CameraOptions
    {
        Rear,
        Front,
    }

    public class CameraPreview : View
    {
        public static readonly BindableProperty CameraProperty
            = BindableProperty.Create(propertyName: nameof(Camera), returnType: typeof(CameraOptions), declaringType: typeof(CameraPreview), defaultValue: CameraOptions.Rear);

        public CameraOptions Camera
        {
            get { return (CameraOptions)GetValue(CameraProperty); }
            set { SetValue(CameraProperty, value); }
        }

        public Command CameraClick { get; set; }

        public void PictureTaken()
        {
            PictureFinished?.Invoke();
        }

        public event Action PictureFinished;
    }
}
