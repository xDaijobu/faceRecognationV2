using System;
using Android.Media;

namespace faceRecognationV2.Droid.Camera2
{
    /// <summary>
    /// setiap x gambar di ambil akan masuk ke sini
    /// logic utk detect face dll hrusny msuk ke sini jg
    /// </summary>
    public class ImageAvailableListener : Java.Lang.Object, ImageReader.IOnImageAvailableListener
    {
        public event EventHandler<byte[]> Photo;

        public void OnImageAvailable(ImageReader reader)
        {
            Image image = null;

            try
            {
                image = reader.AcquireLatestImage();
                var buffer = image.GetPlanes()[0].Buffer;
                var imageData = new byte[buffer.Capacity()];
                buffer.Get(imageData);

                //diremark dl
                //Photo?.Invoke(this, imageData);

            }
            catch (Exception)
            {
                //do nothing
            }
            finally
            {
                image?.Close();
            }
        }
    }
}
