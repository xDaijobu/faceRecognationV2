using System;
using System.Collections.Generic;
using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Media;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using faceRecognationV2.Controls;
using faceRecognationV2.Droid.CustomViews;
using faceRecognationV2.Interfaces;
using Java.Lang;
using Java.Util;
using static faceRecognationV2.Interfaces.ISimilarityClassifier;
using Size = Android.Util.Size;
using Device = Xamarin.Forms.Device;
using faceRecognationV2.Droid.Utils;

namespace faceRecognationV2.Droid.Camera2
{
    public class CameraDroid : FrameLayout, TextureView.ISurfaceTextureListener
    {
        #region Camera States
        // Camera state: Showing camera preview.
        public const int STATE_PREVIEW = 0;

        // Camera state: Waiting for the focus to be locked.
        public const int STATE_WAITING_LOCK = 1;

        // Camera state: Waiting for the exposure to be precapture state.
        public const int STATE_WAITING_PRECAPTURE = 2;

        //Camera state: Waiting for the exposure state to be something other than precapture.
        public const int STATE_WAITING_NON_PRECAPTURE = 3;

        // Camera state: Picture was taken.
        public const int STATE_PICTURE_TAKEN = 4;

        #endregion;

        private float MINIMUM_CONFIDENCE_TF_OD_API = 0.5f;
        private static DetectorMode MODE = DetectorMode.TF_OD_API;
        /// <summary>
        /// The current state of camera state for taking pictures.
        /// </summary>
        public int mState = STATE_PREVIEW;

        private static readonly SparseIntArray ORIENTATIONS = new SparseIntArray();

        public event EventHandler<byte[]> Photo;

        public bool OpeningCamera { private get; set; }

        public CameraDevice CameraDevice;

        private readonly CameraStateListener _cameraStateListener;
        private readonly CameraCaptureListener _cameraCaptureListener;

        private CaptureRequest.Builder _previewBuilder;
        private CaptureRequest.Builder _captureBuilder;
        private CaptureRequest _previewRequest;
        private CameraCaptureSession _previewSession;
        private SurfaceTexture _viewSurface;
        //private readonly TextureView _cameraTexture;
        public readonly AutoFitTextureView _cameraTexture;
        internal CaptureResult CaptureResult { get; set; }
        public Size _previewSize;
        private readonly Context _context;
        private CameraManager _manager;

        private bool mFaceDetectSupported;
        private int mFaceDetectMode;

        private bool _flashSupported;
        private Size[] _supportedJpegSizes;
        private Size _idealPhotoSize = new Size(480, 640);

        private HandlerThread _backgroundThread;
        private Handler _backgroundHandler;

        private ImageReader _imageReader;
        private string _cameraId;

        private LensFacing lensFacing;

        public FaceBoundsView _faceDetectBoundsView { get; private set; }

        public event EventHandler FrameCountUpdated;
        protected virtual void OnFrameCountUpdated(EventArgs e)
        {
            FrameCountUpdated?.Invoke(this, e);
        }

        public event EventHandler SensorOrientationUpdated;
        protected virtual void OnSensorOrientationUpdated(EventArgs e)
        {
            SensorOrientationUpdated?.Invoke(this, e);
        }

        private long _frameCount;
        public long FrameCount
        {
            get
            {
                return _frameCount;
            }
            set
            {
                _frameCount = value;
                OnFrameCountUpdated(EventArgs.Empty);
            }
        }

        private int _sensorOrientation;
        internal int SensorOrientation
        {
            get
            {
                return _sensorOrientation;
            }
            set
            {
                _sensorOrientation = value;
                OnSensorOrientationUpdated(EventArgs.Empty);
            }
        }

        private Matrix frameToCropTransform;
        private Matrix cropToFrameTransform;

        private long lastProcessingTimeMs;
        private Bitmap rgbFrameBitmap = null;
        private Bitmap croppedBitmap = null;
        private Bitmap cropCopyBitmap = null;

        /// <summary>
        /// here the preview image is drawn in portrait way
        /// </summary>
        internal Bitmap portraitBitmap = null;

        /// <summary>
        /// here the face is cropped and drawn
        /// </summary>
        internal Bitmap faceBitmap = null;

        private MultiBoxTracker tracker;
        OverlayView trackingOverlay;

        private bool computingDetection = false;

        private int TF_OD_API_INPUT_SIZE = 112;
        private bool TF_OD_API_IS_QUANTIZED = false;
        private string TF_OD_API_MODEL_FILE = "mobile_face_net.tflite";
        private string TF_OD_API_LABELS_FILE = "file:///android_asset/labelmap.txt";

        private ISimilarityClassifier detector;

        public void SetCameraOption(CameraOptions cameraOptions)
        {
            lensFacing = (cameraOptions == CameraOptions.Front) ? LensFacing.Front : LensFacing.Back;
        }

        public CameraDroid(Context context) : base(context)
        {
            _context = context;

            var inflater = LayoutInflater.FromContext(context);

            if (inflater == null)
                return;


            var view = inflater.Inflate(Resource.Layout.CameraLayout, this);

            _cameraTexture = view.FindViewById<AutoFitTextureView>(Resource.Id.CameraTexture);
            //_faceDetectBoundsView = view.FindViewById<FaceBoundsView>(Resource.Id.FaceDetectBounds);
            trackingOverlay = view.FindViewById<OverlayView>(Resource.Id.TrackingOverlay);

            _cameraTexture.SurfaceTextureListener = this;

            _cameraStateListener = new CameraStateListener { Camera = this };

            _cameraCaptureListener = new CameraCaptureListener(this);

            tracker = new MultiBoxTracker(context);
        }

        public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
        {
            _viewSurface = surface;

            StartBackgroundThread();

            OpenCamera(width, height);
        }

        public bool OnSurfaceTextureDestroyed(SurfaceTexture surface)
        {
            StopBackgroundThread();

            return true;
        }

        public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height)
        {

        }

        public void OnSurfaceTextureUpdated(SurfaceTexture surface)
        {

        }



        //public override onpreview

        private void SetUpCameraOutputs(int width, int height)
        {
            _manager = (CameraManager)_context.GetSystemService(Context.CameraService);

            string[] cameraIds = _manager.GetCameraIdList();

            _cameraId = cameraIds[0];

            for (int i = 0; i < cameraIds.Length; i++)
            {
                CameraCharacteristics chararc = _manager.GetCameraCharacteristics(cameraIds[i]);

                SensorOrientation = (int)chararc.Get(CameraCharacteristics.SensorOrientation);//Back:4032*3024,Front:3264*2448

                int[] faceDetector = (int[])chararc.Get(CameraCharacteristics.StatisticsInfoAvailableFaceDetectModes);
                int maxFaceDetector = (int)chararc.Get(CameraCharacteristics.StatisticsInfoMaxFaceCount);

                if (faceDetector != null)
                {
                    List<Integer> faceDetectorList = new List<Integer>();
                    foreach (var faceD in faceDetector)
                        faceDetectorList.Add((Integer)faceD);

                    if (maxFaceDetector > 0)
                    {
                        mFaceDetectSupported = true;
                        mFaceDetectMode = (int)Collections.Max(faceDetectorList);
                    }
                }

                var facing = (Integer)chararc.Get(CameraCharacteristics.LensFacing);
                if (facing != null && facing == (Integer.ValueOf((int)LensFacing.Back)))
                {
                    _cameraId = cameraIds[i];

                    //Phones like Galaxy S10 have 2 or 3 frontal cameras usually the one with flash is the one
                    //that should be chosen, if not It will select the first one and that can be the fish
                    //eye camera
                    if (HasFLash(chararc))
                        break;
                }
            }

            var characteristics = _manager.GetCameraCharacteristics(_cameraId);
            var map = (StreamConfigurationMap)characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);

            //if (_supportedJpegSizes == null && characteristics != null)
            //{
            //    _supportedJpegSizes = ((StreamConfigurationMap)characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap)).GetOutputSizes((int)ImageFormatType.Jpeg);
            //}

            //if (_supportedJpegSizes != null && _supportedJpegSizes.Length > 0)
            //{
            //    _idealPhotoSize = GetOptimalSize(_supportedJpegSizes, 1050, 1400); //MAGIC NUMBER WHICH HAS PROVEN TO BE THE BEST
            //}

            //_imageReader = ImageReader.NewInstance(_idealPhotoSize.Width, _idealPhotoSize.Height, ImageFormatType.Jpeg, 1);
            _previewSize = map.GetOutputSizes((int)ImageFormatType.Jpeg)[0];

            rgbFrameBitmap = Bitmap.CreateBitmap(_previewSize.Width, _previewSize.Height, Bitmap.Config.Argb8888);

            int targetW, targetH;
            if (SensorOrientation == 90 || SensorOrientation == 270)
            {
                targetH = _previewSize.Width;
                targetW = _previewSize.Height;
            }
            else
            {
                targetW = _previewSize.Width;
                targetH = _previewSize.Height;
            }
            int cropW = (int)(targetW / 2.0);
            int cropH = (int)(targetH / 2.0);

            croppedBitmap = Bitmap.CreateBitmap(cropW, cropH, Bitmap.Config.Argb8888);

            portraitBitmap = Bitmap.CreateBitmap(targetW, targetH, Bitmap.Config.Argb8888);
            faceBitmap = Bitmap.CreateBitmap(TF_OD_API_INPUT_SIZE, TF_OD_API_INPUT_SIZE, Bitmap.Config.Argb8888);

            frameToCropTransform =
                    ImageUtils.GetTransformationMatrix(
                            _previewSize.Width, _previewSize.Height,
                            cropW, cropH,
                            SensorOrientation, false /*MAINTAIN_ASPECT*/);


            cropToFrameTransform = new Matrix();
            frameToCropTransform.Invert(cropToFrameTransform);

            _imageReader = ImageReader.NewInstance(480, 680, ImageFormatType.Jpeg, 1);
            var readerListener = new ImageAvailableListener();

            readerListener.Photo += (sender, buffer) =>
            {
                Photo?.Invoke(this, buffer);
            };

            _flashSupported = HasFLash(characteristics);

            _imageReader.SetOnImageAvailableListener(readerListener, _backgroundHandler);

            //_previewSize = GetOptimalSize(map.GetOutputSizes(Class.FromType(typeof(SurfaceTexture))), width, height);
        }

        private bool HasFLash(CameraCharacteristics characteristics)
        {
            var available = (Java.Lang.Boolean)characteristics.Get(CameraCharacteristics.FlashInfoAvailable);
            if (available == null)
            {
                return false;
            }
            else
            {
                return (bool)available;
            }
        }

        public void OpenCamera(int width, int height)
        {
            if (_context == null || OpeningCamera)
            {
                return;
            }

            OpeningCamera = true;

            SetUpCameraOutputs(width, height);


            _manager.OpenCamera(_cameraId, _cameraStateListener, null);
        }

        public void TakePhoto()
        {
            if (_context == null || CameraDevice == null) return;

            if (_captureBuilder == null)
                _captureBuilder = CameraDevice.CreateCaptureRequest(CameraTemplate.StillCapture);

            _captureBuilder.AddTarget(_imageReader.Surface);

            _captureBuilder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousPicture);
            SetAutoFlash(_captureBuilder);

            _previewSession.StopRepeating();
            _previewSession.Capture(_captureBuilder.Build(),
                new CameraCaptureStillPictureSessionCallback
                {
                    OnCaptureCompletedAction = session =>
                    {
                        UnlockFocus();
                    }
                }, null);
        }

        public void StartPreview()
        {
            if (CameraDevice == null || !_cameraTexture.IsAvailable || _previewSize == null) return;

            var texture = _cameraTexture.SurfaceTexture;

            texture.SetDefaultBufferSize(_previewSize.Width, _previewSize.Height);

            var surface = new Surface(texture);

            _previewBuilder = CameraDevice.CreateCaptureRequest(CameraTemplate.Preview);
            _previewBuilder.AddTarget(surface);

            System.Diagnostics.Debug.WriteLine("Openning camera preview : " + _previewSize.Width + "x" + _previewSize.Height);

            List<Surface> surfaces = new List<Surface>();
            surfaces.Add(surface);
            surfaces.Add(_imageReader.Surface);

            // Here, we create a CameraCaptureSession for camera preview
            CameraDevice.CreateCaptureSession(surfaces,
                new CameraCaptureStateListener
                {
                    OnConfigureFailedAction = session =>
                    {
                        System.Diagnostics.Debug.WriteLine("Failed.");
                    },
                    OnConfiguredAction = session =>
                    {
                        _previewSession = session;
                        UpdatePreview();
                    }
                },
                _backgroundHandler);
        }

        private void UpdatePreview()
        {
            if (CameraDevice == null || _previewSession == null)
                return;

            // Reset the auto-focus trigger
            _previewBuilder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousPicture);
            SetAutoFlash(_previewBuilder);

            SetFaceDetect(_previewBuilder, mFaceDetectMode);

            _previewRequest = _previewBuilder.Build();
            _previewSession.SetRepeatingRequest(_previewRequest, _cameraCaptureListener, _backgroundHandler);
        }

        Size GetOptimalSize(IList<Size> sizes, int h, int w)
        {
            double AspectTolerance = 0.1;
            double targetRatio = (double)w / h;

            if (sizes == null)
            {
                return null;
            }

            Size optimalSize = null;
            double minDiff = double.MaxValue;
            int targetHeight = h;

            while (optimalSize == null)
            {
                foreach (Size size in sizes)
                {
                    double ratio = (double)size.Width / size.Height;

                    if (System.Math.Abs(ratio - targetRatio) > AspectTolerance)
                        continue;
                    if (System.Math.Abs(size.Height - targetHeight) < minDiff)
                    {
                        optimalSize = size;
                        minDiff = System.Math.Abs(size.Height - targetHeight);
                    }
                }

                if (optimalSize == null)
                    AspectTolerance += 0.1f;
            }

            return optimalSize;
        }

        public void SetAutoFlash(CaptureRequest.Builder requestBuilder)
        {
            if (_flashSupported)
            {
                requestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.OnAutoFlash);
            }
        }

        public void SetFaceDetect(CaptureRequest.Builder requestBuilder, int faceDetectMode)
        {
            if (mFaceDetectSupported)
                requestBuilder.Set(CaptureRequest.StatisticsFaceDetectMode, faceDetectMode);
        }

        private void StartBackgroundThread()
        {
            _backgroundThread = new HandlerThread("CameraBackground");
            _backgroundThread.Start();
            _backgroundHandler = new Handler(_backgroundThread.Looper);
        }

        private void StopBackgroundThread()
        {
            _backgroundThread.QuitSafely();
            try
            {
                _backgroundThread.Join();
                _backgroundThread = null;
                _backgroundHandler = null;
            }
            catch (InterruptedException e)
            {
                e.PrintStackTrace();
            }
        }

        public void LockFocus()
        {
            try
            {
                // This is how to tell the camera to lock focus.
                _previewBuilder.Set(CaptureRequest.ControlAfTrigger, (int)ControlAFTrigger.Start);
                // Tell #mCaptureCallback to wait for the lock.
                mState = STATE_WAITING_LOCK;
                _previewSession.Capture(_previewBuilder.Build(), _cameraCaptureListener, _backgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        public void UnlockFocus()
        {
            try
            {
                // Reset the auto-focus trigger
                _previewBuilder.Set(CaptureRequest.ControlAfTrigger, (int)ControlAFTrigger.Cancel);
                SetAutoFlash(_previewBuilder);

                _previewSession.Capture(_previewBuilder.Build(), _cameraCaptureListener, _backgroundHandler);
                // After this, the camera will go back to the normal state of preview.
                mState = STATE_PREVIEW;
                _previewSession.SetRepeatingRequest(_previewRequest, _cameraCaptureListener, _backgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        public void RunPrecaptureSequence()
        {
            try
            {
                _previewBuilder.Set(CaptureRequest.ControlAePrecaptureTrigger, (int)ControlAEPrecaptureTrigger.Start);
                mState = STATE_WAITING_PRECAPTURE;
                _previewSession.Capture(_previewBuilder.Build(), _cameraCaptureListener, _backgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        public void OnFacesDetected(long currTimestamp, Face[] faces, bool add)
        {
            cropCopyBitmap = Bitmap.CreateBitmap(croppedBitmap);
            Canvas canvas = new Canvas(cropCopyBitmap);
            Paint paint = new Paint();
            paint.Color = Color.Red;
            paint.SetStyle(Paint.Style.Stroke);
            paint.StrokeWidth = 2.0f;

            float minimumConfidence = MINIMUM_CONFIDENCE_TF_OD_API;

            switch (MODE)
            {
                case DetectorMode.TF_OD_API:
                    minimumConfidence = MINIMUM_CONFIDENCE_TF_OD_API;
                    break;
            }

            List<Recognition> mappedRecognitions = new List<Recognition>();

            // Note this can be done only once
            int sourceW = rgbFrameBitmap.Width;
            int sourceH = rgbFrameBitmap.Height;
            int targetW = portraitBitmap.Width;
            int targetH = portraitBitmap.Height;

            Matrix transform = CreateTransform(sourceW, sourceH, targetW, targetH, SensorOrientation);
            Canvas cv = new Canvas(portraitBitmap);

            // draws the original image in portrait mode.
            cv.DrawBitmap(rgbFrameBitmap, transform, null);

            Canvas cvFace = new Canvas(faceBitmap);

            bool saved = false;

            foreach (Face face in faces)
            {
                System.Diagnostics.Debug.WriteLine($"FACE: {face}");
                System.Diagnostics.Debug.WriteLine($"Running detection on face {currTimestamp}");

                RectF boundingBox = new RectF(face.Bounds);

                if (boundingBox != null)
                {
                    //maps crop coordinates to original
                    cropToFrameTransform.MapRect(boundingBox);

                    // maps original coordinates to portrait coordinates
                    RectF faceBB = new RectF(boundingBox);
                    transform.MapRect(faceBB);

                    // translates portrait to origin and scales to fit input inference size
                    float sx = ((float)TF_OD_API_INPUT_SIZE) / faceBB.Width();
                    float sy = ((float)TF_OD_API_INPUT_SIZE) / faceBB.Height();
                    Matrix matrix = new Matrix();
                    matrix.PostTranslate(-faceBB.Left, -faceBB.Top);
                    matrix.PostScale(sx, sy);

                    cvFace.DrawBitmap(portraitBitmap, matrix, null);

                    string label = "";
                    float confidence = -1f;
                    int color = Color.Blue;
                    object extra = null;
                    Bitmap crop = null;

                    if (add)
                    {
                        crop = Bitmap.CreateBitmap(portraitBitmap,
                            (int)faceBB.Left,
                            (int)faceBB.Top,
                            (int)faceBB.Width(),
                            (int)faceBB.Height());
                    }

                    long startTime = SystemClock.UptimeMillis();
                    List<Recognition> resultsAux = detector.RecognizeImage(faceBitmap, add);
                    lastProcessingTimeMs = SystemClock.UptimeMillis() - startTime;

                    if (resultsAux.Count > 0)
                    {
                        Recognition _result = resultsAux[0];

                        extra = _result.Extra;
                        float conf = _result.Distance.Value;
                        if (conf < 1.0f)
                        {
                            confidence = conf;
                            label = _result.Title;
                            if (_result.Id.Equals("0"))
                                color = Color.Green;
                            else
                                color = Color.Red;
                        }
                    }

                    if (lensFacing == LensFacing.Front)
                    {
                        // camera is frontal so image is flipped horizontally
                        // flips horizontally
                        Matrix flip = new Matrix();
                        if (SensorOrientation == 90 || SensorOrientation == 270)
                        {
                            flip.PostScale(1, -1, _previewSize.Width / 2.0f, _previewSize.Height / 2.0f);
                        }
                        else
                        {
                            flip.PostScale(-1, 1, _previewSize.Width / 2.0f, _previewSize.Height / 2.0f);
                        }
                        //flip.postScale(1, -1, targetW / 2.0f, targetH / 2.0f);
                        flip.MapRect(boundingBox);
                    }

                    Recognition result = new Recognition("0", label, confidence, boundingBox);
                    result.Color = color;
                    result.Location = boundingBox;
                    result.Extra = extra;
                    result.Crop = crop;
                    mappedRecognitions.Add(result);
                }
            }

            UpdateResults(currTimestamp, mappedRecognitions);
        }
        
  //      private void updateResults(long currTimestamp, final List<SimilarityClassifier.Recognition> mappedRecognitions)
  //      {

  //          tracker.trackResults(mappedRecognitions, currTimestamp);
  //          trackingOverlay.postInvalidate();
  //          computingDetection = false;
  //          //adding = false;


  //          if (mappedRecognitions.size() > 0)
  //          {
  //              LOGGER.i("Adding results");
  //              SimilarityClassifier.Recognition rec = mappedRecognitions.get(0);
  //              if (rec.getExtra() != null)
  //              {
  //                  showAddFaceDialog(rec);
  //              }

  //          }

  //          runOnUiThread(
  //                  new Runnable() {
  //            @Override
  //                    public void run()
  //          {
  //              showFrameInfo(previewWidth + "x" + previewHeight);
  //              showCropInfo(croppedBitmap.getWidth() + "x" + croppedBitmap.getHeight());
  //              showInference(lastProcessingTimeMs + "ms");
  //          }
  //      });

  //}

        private void UpdateResults(long currTimestamp, List<Recognition> mappedRecognitions)
        {
            tracker.TrackResults(mappedRecognitions, currTimestamp);
            trackingOverlay.PostInvalidate();
            computingDetection = false;

            if (mappedRecognitions.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine("Adding results");

                Recognition recognition = mappedRecognitions[0];
                if (recognition.Extra != null)
                {
                    ShowAddFaceDialog(recognition);
                }
            }

            Device.BeginInvokeOnMainThread(() =>
            {
                System.Diagnostics.Debug.WriteLine("ShowFrameInfo: " + _previewSize.Width + "x" + _previewSize.Height);
                System.Diagnostics.Debug.WriteLine("ShowCropInfo: " + croppedBitmap.Width + "x" + croppedBitmap.Height);
                System.Diagnostics.Debug.WriteLine("ShowInference: " + lastProcessingTimeMs + "ms");
                //ShowFrameInfo(_previewSize.Width + "x" + _previewSize.Height); ;
                //ShowCropInfo(croppedBitmap.Width + "x" + croppedBitmap.Height);
                //ShowInference(lastProcessingTimeMs + "ms");
            });
        }

        private Matrix CreateTransform(int sourceWidth, int sourceHeight, int destinationWidth, int destinationHeight, int applyRotation)
        {
            Matrix matrix = new Matrix();
            if (applyRotation != 0)
            {
                if (applyRotation % 90 != 0)
                    System.Diagnostics.Debug.WriteLine("Rotation of %d % 90 != 0", applyRotation);

                // Tarnslate so center of image is at origin
                matrix.PostTranslate(-sourceWidth / 2.0f, -sourceHeight / -2.0f);

                // Rotate around origin.
                matrix.PostRotate(applyRotation);
            }

            if (applyRotation != 0)
            {
                // Translate back from origin centered reference to destination frame.
                matrix.PostTranslate(destinationWidth / 2.0f, destinationHeight / 2.0f);
            }

            return matrix;
        }

        //TODO CRIS 
        private void ShowAddFaceDialog(Recognition recognition)
        {
            /*
                AlertDialog.Builder builder = new AlertDialog.Builder(this);
                LayoutInflater inflater = getLayoutInflater();
                View dialogLayout = inflater.inflate(R.layout.image_edit_dialog, null);
                ImageView ivFace = dialogLayout.findViewById(R.id.dlg_image);
                TextView tvTitle = dialogLayout.findViewById(R.id.dlg_title);
                EditText etName = dialogLayout.findViewById(R.id.dlg_input);

                tvTitle.setText("Add Face");
                ivFace.setImageBitmap(rec.getCrop());
                etName.setHint("Input name");

                builder.setPositiveButton("OK", new DialogInterface.OnClickListener(){
                    @Override
                    public void onClick(DialogInterface dlg, int i) {

                        String name = etName.getText().toString();
                        if (name.isEmpty()) {
                            return;
                        }
                        detector.register(name, rec);
                        //knownFaces.put(name, rec);
                        dlg.dismiss();
                    }
                });
                builder.setView(dialogLayout);
                builder.show();
             */
        }

        // Which detection model to use: by default uses Tensorflow Object Detection API frozen
        // checkpoints.
        private enum DetectorMode
        {
            TF_OD_API,
        }
    }


}
