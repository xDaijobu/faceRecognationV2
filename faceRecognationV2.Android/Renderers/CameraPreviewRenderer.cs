using Android.Content;
using faceRecognationV2.Controls;
using faceRecognationV2.Droid.Camera2;
using faceRecognationV2.Droid.Renderers;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;

[assembly: ExportRenderer(typeof(CameraPreview), typeof(CameraPreviewRenderer))]
namespace faceRecognationV2.Droid.Renderers
{
    public class CameraPreviewRenderer : ViewRenderer<CameraPreview, CameraDroid>
    {
        private CameraDroid _camera;
        private CameraPreview _currentElement;
        private readonly Context _context;

        public CameraPreviewRenderer(Context context) : base(context)
        {
            _context = context;
        }

        protected override void OnElementChanged(ElementChangedEventArgs<CameraPreview> e)
        {
            base.OnElementChanged(e);

            _camera = new CameraDroid(Context);

            SetNativeControl(_camera);

            if (e.NewElement != null && _camera != null)
            {
                e.NewElement.CameraClick = new Command(() => TakePicture());
                _currentElement = e.NewElement;
                _camera.SetCameraOption(_currentElement.Camera);
                _camera.Photo += OnPhoto;
            }
        }

        public void TakePicture() => _camera.LockFocus();

        private void OnPhoto(object sender, byte[] imageSource)
        {
            //here you have the image byte data to do what ever you want

            Device.BeginInvokeOnMainThread(() =>
            {
                _currentElement?.PictureTaken();
            });
        }

        protected override void Dispose(bool disposing)
        {
            _camera.Photo -= OnPhoto;

            base.Dispose(disposing);
        }
    }
}
