using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Android.Content.Res;
using Android.Graphics;
using faceRecognationV2.Interfaces;
using Java.IO;
using Java.Lang;
using Java.Nio;
using Java.Nio.Channels;
using Xamarin.TensorFlow.Lite;
using Xamarin.TensorFlow.Lite.GPU;
using static faceRecognationV2.Interfaces.ISimilarityClassifier;
using Bitmap = Android.Graphics.Bitmap;
using Debug = System.Diagnostics.Debug;
using Object = Java.Lang.Object;
using Trace = Android.OS.Trace;

namespace faceRecognationV2.Droid.Models
{
    public class TFLiteObjectDetectionAPIModel : ISimilarityClassifier
    {
        private static int OUTPUT_SIZE = 192;

        /// <summary>
        /// Only return this many results.
        /// </summary>
        private static int NUM_DETECTIONS = 1;

        //float model
        private static float IMAGE_MEAN = 128.0f;
        private static float IMAGE_STD = 128.0f;

        // Number of the threads in the xamarin android app
        private static int NUM_THREADS = 4;
        private bool isModelQuantized = false;

        // Config values.
        private int inputSize;
        // Pre-allocated buffers.
        private List<string> labels = new List<string>();
        private int[] intValues;
        // outputLocations: array of shape [Batchsize, NUM_DETECTIONS,4]
        // contains the location of detected boxes
        private float[,,] outputLocations;
        // outputClasses: array of shape [Batchsize, NUM_DETECTIONS]
        // contains the classes of detected boxes
        private float[,] outputClasses;
        // outputScores: array of shape [Batchsize, NUM_DETECTIONS]
        // contains the scores of detected boxes
        private float[,] outputScores;
        // numDetections: array of shape [Batchsize]
        // contains the number of detected boxes
        private float[] numDetections;

        private float[,] embeedings;

        private ByteBuffer imgData;

        private Interpreter tfLite;

        // Face Mask Detector Output
        private float[][] output;

        private Dictionary<string, Recognition> registered = new Dictionary<string, Recognition>();
        public void Register(string name, Recognition rec) => registered.Add(name, rec);

        private TFLiteObjectDetectionAPIModel()
        {
        }

        /** Memory-map the model file in Assets. */
        private static MappedByteBuffer LoadModelFile(AssetManager assets, string modelFilename)
        {
            AssetFileDescriptor fileDescriptor = assets.OpenFd(modelFilename);
            FileInputStream inputStream = new FileInputStream(fileDescriptor.FileDescriptor);
            FileChannel fileChannel = inputStream.Channel;
            long startOffset = fileDescriptor.StartOffset;
            long declaredLength = fileDescriptor.DeclaredLength;
            return fileChannel.Map(FileChannel.MapMode.ReadOnly, startOffset, declaredLength);
        }

        public static ISimilarityClassifier Create(AssetManager assetManager, string modelFilename, string labelFilename, int inputSize, bool isQuantized)
        {
            TFLiteObjectDetectionAPIModel d = new TFLiteObjectDetectionAPIModel();

            string actualFilename = labelFilename.Split("file:///android_asset/")[1];
            Stream labelsInput = assetManager.Open(actualFilename);
            BufferedReader bufferedReader = new BufferedReader(new InputStreamReader(labelsInput));
            string line;
            while ((line = bufferedReader.ReadLine()) != null)
            {
                Debug.WriteLine(line);
                d.labels.Add(line);
            }
            bufferedReader.Close();

            d.inputSize = inputSize;

            try
            {
                GpuDelegate gpuDelegate = new GpuDelegate();
                MappedByteBuffer mappedByteBuffer = LoadModelFile(assetManager, actualFilename);
                Interpreter.Options options = new Interpreter.Options().AddDelegate(gpuDelegate);
                options.SetNumThreads(NUM_THREADS);
                d.tfLite = new Interpreter(mappedByteBuffer, options);
            }
            catch (Java.Lang.Exception e)
            {
                throw new RuntimeException(e);
            }

            d.isModelQuantized = isQuantized;
            // Pre-allocate buffers.
            int numBytesPerChannel;
            if (isQuantized)
            {
                numBytesPerChannel = 1; // Quantized
            }
            else
            {
                numBytesPerChannel = 4; // Floating point
            }
            d.imgData = ByteBuffer.AllocateDirect(1 * d.inputSize * d.inputSize * 3 * numBytesPerChannel);
            d.imgData.Order(ByteOrder.NativeOrder());
            d.intValues = new int[d.inputSize * d.inputSize];

            //deprecated dari Interpreter.SetNumThreads
            //new api Interpreter.Options.SetNumThreads
            //d.tfLite.SetNumThreads(NUM_THREADS);

            d.outputLocations = new float[1, NUM_DETECTIONS, 4];
            //d.outputLocations = new Float[1][1][1];
            d.outputClasses = new float[1, NUM_DETECTIONS];
            d.outputScores = new float[1, NUM_DETECTIONS];
            d.numDetections = new float[1];
            return d;
        }

        // looks for the nearest embeeding in the dataset (using L2 norm)
        // and retrurns the pair <id, distance>
        private (string, float) FindNearest(float[] emb)
        {
            (string, float) tuple = (null, 0);

            foreach (var entry in registered)
            {
                string name = entry.Key;
                float[] knownEmb = ((float[][])entry.Value.Extra)[0];

                float distance = 0;

                for (int i = 0; i < emb.Length; i++)
                {
                    float diff = emb[i] - knownEmb[i];
                    distance += diff * diff;
                }

                distance = (float)System.Math.Sqrt(distance);

                if (string.IsNullOrEmpty(tuple.Item1) && tuple.Item2 == 0 || distance < tuple.Item2)
                    tuple = (name, distance);
            }

            return tuple;
        }

        public List<Recognition> RecognizeImage(Bitmap bitmap, bool storeExtra)
        {
            // Log this method so that it can be analyzed with systrace.
            Trace.BeginSection("recognizeImage");

            Trace.BeginSection("preprocessBitmap");

            // Preprocess the image data from 0-255 int to normalized float based
            // on the provided parameters.
            bitmap.GetPixels(intValues, 0, bitmap.Width, 0, 0, bitmap.Width, bitmap.Height);

            imgData.Rewind();
            for (int i = 0; i < inputSize; ++i)
            {
                for (int j = 0; j < inputSize; ++j)
                {
                    int pixelValue = intValues[i * inputSize + j];
                    if (isModelQuantized)
                    {
                        // Quantized model
                        imgData.Put((sbyte)((pixelValue >> 16) & 0xFF));
                        imgData.Put((sbyte)((pixelValue >> 8) & 0xFF));
                        imgData.Put((sbyte)(pixelValue & 0xFF));
                    }
                    else
                    { // Float model
                        imgData.PutFloat((((pixelValue >> 16) & 0xFF) - IMAGE_MEAN) / IMAGE_STD);
                        imgData.PutFloat((((pixelValue >> 8) & 0xFF) - IMAGE_MEAN) / IMAGE_STD);
                        imgData.PutFloat(((pixelValue & 0xFF) - IMAGE_MEAN) / IMAGE_STD);
                    }
                }
            }

            Trace.EndSection(); // preprocessBitmap

            // Copy the input data into TensorFlow.
            Trace.BeginSection("feed");

            Object[] inputArray = { imgData };

            Trace.EndSection();

            // Here outputMap is changed to fit the Face Mask detector
            Dictionary<int, object> outputMap = new Dictionary<int, object>();

            embeedings = new float[1,OUTPUT_SIZE];
            outputMap.Add(0, embeedings);

            // Run the inference call.
            Trace.BeginSection("run");
            //tfLite.runForMultipleInputsOutputs(inputArray, outputMapBack);
            tfLite.RunForMultipleInputsOutputs(inputArray, (IDictionary<Integer, Object>)outputMap);
            Trace.EndSection();

            float distance = float.MaxValue;
            string id = "0";
            string label = "?";

            if (registered.Count > 0)
            {
                //LOGGER.i("dataset SIZE: " + registered.size());
                (string, float) nearest = FindNearest(embeedings.GetRow(0));
                //if (nearest != null)
                if (!string.IsNullOrEmpty(nearest.Item1) && nearest.Item2 > 0)
                {
                    string name = nearest.Item1;
                    label = name;
                    distance = nearest.Item2;

                    Debug.WriteLine("nearest : " + name + " - distance : " + distance);
                }
            }

            int numDetectionsOutput = 1;
            List<Recognition> recognitions = new List<Recognition>(numDetectionsOutput);
            Recognition rec = new Recognition(id, label, distance, new RectF());

            recognitions.Add(rec);

            if (storeExtra)
                rec.Extra = embeedings;

            Trace.EndSection();
            return recognitions;
        }

        public void EnableStatLogging(bool debug)
        {
        }

        public string GetStatString()
        {
            return "";
        }

        public void Close()
        {
        }

        public void SetNumThreads(int numberThreads)
        {
            if (tfLite != null)
                tfLite.SetNumThreads(numberThreads);
        }

        public void SetUseNNAPI(bool isChecked)
        {
            //if (tfLite != null)
            //    tfLite.SetUseNNAPI(isChecked);
        }
    }

    public static class ArrayExt
    {
        public static T[] GetRow<T>(this T[,] array, int row)
        {
            if (!typeof(T).IsPrimitive)
                throw new InvalidOperationException("Not supported for managed types.");

            if (array == null)
                throw new ArgumentNullException("array");

            int cols = array.GetUpperBound(1) + 1;
            T[] result = new T[cols];

            int size;

            if (typeof(T) == typeof(bool))
                size = 1;
            else if (typeof(T) == typeof(char))
                size = 2;
            else
                size = Marshal.SizeOf<T>();

            System.Buffer.BlockCopy(array, row * cols * size, result, 0, cols * size);

            return result;
        }
    }
}
