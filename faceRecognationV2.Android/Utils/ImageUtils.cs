using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Android.Graphics;
using Java.Util.Logging;

namespace faceRecognationV2.Droid.Utils
{
    // Ref : https://github.com/estebanuri/face_recognition/blob/36b221546f3ce2ad591a9e21bfbd8dc6c1134d24/android/app/src/main/java/org/tensorflow/lite/examples/detection/env/ImageUtils.java

    /// <summary>
    /// Utility class for manipulating images
    /// </summary>
    public class ImageUtils
    {
        /// <summary>
        /// This value is 2 ^ 18 - 1, and is used to clamp the RGB values before their ranges
        /// are normalized to eight bits.
        /// </summary>
        static int kMaxChannelValue = 262143;

        private static Logger LOGGER;

        /// <summary>
        /// Utility method to compute the allocated size in bytes of a YUV420SP image of the given dimensions.
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static int GetYUVByteSize(int width, int height)
        {
            //The luminance plane requires 1 byte per pixel.
            int ySize = width * height;

            // The UV plane works on 2x2 blocks, so dimensions with odd size must be rounded up.
            // Each 2x2 block takes 2 bytes to encode, one each for U and V.
            int uvSize = (width + 1) / 2 * ((height + 1) / 2) * 2;

            return ySize + uvSize;
        }

        [Obsolete]
        public static async Task SaveBitmap(Bitmap bitmap) => await SaveBitmap(bitmap, "preview.png");

        [Obsolete]
        public static async Task SaveBitmap(Bitmap bitmap, string fileName)
        {
            string root = Android.OS.Environment.ExternalStorageDirectory.AbsolutePath + System.IO.Path.DirectorySeparatorChar + "tensorflow";

            Directory.CreateDirectory(root);

            var stream = System.IO.File.OpenRead(root);

            if (System.IO.File.Exists(root))
                System.IO.File.Delete(root);

            try
            {
                await bitmap.CompressAsync(Bitmap.CompressFormat.Png, 99, stream);
                stream.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception : " + ex);
            }
        }


        public static void ConvertYUV420SPToARGB8888(byte[] input, int width, int height, int[] output)
        {
            int frameSize = width * height;
            for (int j = 0, yp = 0; j < height; j++)
            {
                int uvp = frameSize + (j >> 1) * width;
                int u = 0;
                int v = 0;

                for (int i = 0; i < width; i++, yp++)
                {
                    int y = 0xff & input[yp];
                    if ((i & 1) == 0)
                    {
                        v = 0xff & input[uvp++];
                        u = 0xff & input[uvp++];
                    }

                    output[yp] = YUV2RGB(y, u, v);
                }
            }
        }

        private static int YUV2RGB(int y, int u, int v)
        {
            // Adjust and check YUV values
            y = (y - 16) < 0 ? 0 : (y - 16);
            u -= 128;
            v -= 128;

            // This is the floating point equivalent. We do the conversion in integer
            // because some Android devices do not have floating point in hardware.
            // nR = (int)(1.164 * nY + 2.018 * nU);
            // nG = (int)(1.164 * nY - 0.813 * nV - 0.391 * nU);
            // nB = (int)(1.164 * nY + 1.596 * nV);
            int y1192 = 1192 * y;
            int r = (y1192 + 1634 * v);
            int g = (y1192 - 833 * v - 400 * u);
            int b = (y1192 + 2066 * u);

            // Clipping RGB values to be inside boundaries [ 0 , kMaxChannelValue ]
            r = r > kMaxChannelValue ? kMaxChannelValue : (r < 0 ? 0 : r);
            g = g > kMaxChannelValue ? kMaxChannelValue : (g < 0 ? 0 : g);
            b = b > kMaxChannelValue ? kMaxChannelValue : (b < 0 ? 0 : b);

            return Convert.ToInt32(0xff000000) | ((r << 6) & Convert.ToInt32(0xff0000)) | ((g >> 2) & Convert.ToInt32(0xff00)) | ((b >> 10) & Convert.ToInt32(0xff));
        }

        public static void ConvertYUV420ToARGB8888(byte[] yData, byte[] uData, byte[] vData, int width, int height, int yRowStride, int uvRowStride, int uvPixelStride, ref int[] intOut)
        {
            int yp = 0;
            for (int j = 0; j < height; j++)
            {
                int pY = yRowStride * j;
                int pUV = uvRowStride * (j >> 1);

                for (int i = 0; i < width; i++)
                {
                    int uv_offset = pUV + (i >> 1) * uvPixelStride;
                    intOut[yp++] = YUV2RGB(0xff & yData[pY + i], 0xff & uData[uv_offset], 0xff & vData[uv_offset]);
                }
            }
        }

        /// <summary>
        /// Returns a transformation matrix from one reference frame into another.Handles cropping (if maintaining aspect ratio is desired) and rotation. )
        /// </summary>
        /// <param name="srcWidth">Width of source frame.</param>
        /// <param name="srcHeight">Height of source frame.</param>
        /// <param name="dstWidth">Width of destination frame.</param>
        /// <param name="dstHeight">Height of destination frame.</param>
        /// <param name="applyRotation">Amount of rotation to apply from one frame to another. Must be a multiple of 90.</param>
        /// <param name="maintainAspectRatio">If true, will ensure that scaling in x and y remains constant,, cropping the image if necessary.</param>
        /// <returns>The transformation fulfilling the desired requirements.</returns>
        public static Matrix GetTransformationMatrix(int srcWidth, int srcHeight, int dstWidth, int dstHeight, int applyRotation, bool maintainAspectRatio)
        {
            Matrix matrix = new Matrix();

            if (applyRotation != 0)
            {
                if (applyRotation % 90 != 0)
                {
                    //LOGGER.w("Rotation of %d % 90 != 0", applyRotation);
                    Debug.WriteLine("Rotation of &d % 90 != 0 |", applyRotation);
                }

                // Translate so center of image is at origin.
                matrix.PostTranslate(-srcWidth / 2.0f, -srcHeight / 2.0f);

                // Rotate around origin.
                matrix.PostRotate(applyRotation);
            }

            // Account for the already applied rotation, if any, and then determine how
            // much scaling is needed for each axis.
            bool transpose = (Math.Abs(applyRotation) + 90) % 180 == 0;

            int inWidth = transpose ? srcHeight : srcWidth;
            int inHeight = transpose ? srcWidth : srcHeight;

            // Apply scaling if necessary.
            if (inWidth != dstWidth || inHeight != dstHeight)
            {
                float scaleFactorX = dstWidth / (float)inWidth;
                float scaleFactorY = dstHeight / (float)inHeight;

                if (maintainAspectRatio)
                {
                    // Scale by minimum factor so that dst is filled completely while
                    // maintaining the aspect ratio. Some image may fall off the edge.
                    float scaleFactor = Math.Max(scaleFactorX, scaleFactorY);
                    matrix.PostScale(scaleFactor, scaleFactor);
                }
                else
                {
                    // Scale exactly to fill dst from src.
                    matrix.PostScale(scaleFactorX, scaleFactorY);
                }
            }

            if (applyRotation != 0)
            {
                // Translate back from origin centered reference to destination frame.
                matrix.PostTranslate(dstWidth / 2.0f, dstHeight / 2.0f);
            }

            return matrix;
        }
    }
}
