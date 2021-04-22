using System;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Java.Lang;
using System.Diagnostics;

namespace faceRecognationV2.Droid.Camera2
{
    public class CameraCaptureListener : CameraCaptureSession.CaptureCallback
    {
        private readonly CameraDroid owner;

        public CameraCaptureListener(CameraDroid owner) => this.owner = owner ?? throw new ArgumentNullException("owner");

        public override void OnCaptureCompleted(CameraCaptureSession session, CaptureRequest request,
            TotalCaptureResult result) => Process(result);

        public override void OnCaptureProgressed(CameraCaptureSession session, CaptureRequest request, CaptureResult partialResult) => Process(partialResult);

        private void Process(CaptureResult result)
        {
            switch (owner.mState)
            {
                case CameraDroid.STATE_PREVIEW:
                    {
                        Debug.WriteLine("CameraDroid.STATE_PREVIEW");

                        var f = result.Get(CaptureResult.StatisticsFaces);
                        Face[] faces = f.ToArray<Face>();
                        if (faces.Length > 0)
                        {
                            Debug.WriteLine($"Faces: {faces.Length}");
                            ////faces[0].Bounds a.k.a RECT
                            ////faces[0].Id a.k.a PK
                            owner.mState = CameraDroid.STATE_PICTURE_TAKEN;
                            owner.TakePhoto();

                            System.Diagnostics.Debug.WriteLine("OnFacesDetected");
                            //CameraDetector.OnFacesDetected(1, faces.ToList(), true);

                            //System.Diagnostics.Debug.WriteLine("ShowBoundsOnFace");
                            //owner.FaceDetectBoundsView.ShowBoundsOnFace(faces,
                            //                                  owner._cameraTexture.Width,
                            //                                  owner._cameraTexture.Height,
                            //                                  owner._previewSize.Width,
                            //                                  owner._previewSize.Height,
                            //                                  270);
                            ////owner.SensorOrientation);
                            ////FaceDetector

                        }
                        break;
                    }
                case CameraDroid.STATE_WAITING_LOCK:
                    {
                        Debug.WriteLine("CameraDroid.STATE_WAITING_LOCK");
                        Integer afState = (Integer)result.Get(CaptureResult.ControlAfState);
                        if (afState == null)
                        {
                            owner.mState = CameraDroid.STATE_PICTURE_TAKEN;
                            owner.TakePhoto();
                        }
                        else if ((((int)ControlAFState.FocusedLocked) == afState.IntValue()) ||
                                   (((int)ControlAFState.NotFocusedLocked) == afState.IntValue()))
                        {
                            // ControlAeState can be null on some devices
                            Integer aeState = (Integer)result.Get(CaptureResult.ControlAeState);

                            if (aeState == null || aeState.IntValue() == ((int)ControlAEState.Converged))
                            {
                                owner.mState = CameraDroid.STATE_PICTURE_TAKEN;
                                owner.TakePhoto();
                            }
                            else
                            {
                                owner.RunPrecaptureSequence();
                            }
                        }
                        break;
                    }
                case CameraDroid.STATE_WAITING_PRECAPTURE:
                    {
                        Debug.WriteLine("CameraDroid.STATE_WAITING_PRECAPTURE");
                        // ControlAeState can be null on some devices
                        Integer aeState = (Integer)result.Get(CaptureResult.ControlAeState);
                        if (aeState == null ||
                                aeState.IntValue() == ((int)ControlAEState.Precapture) ||
                                aeState.IntValue() == ((int)ControlAEState.FlashRequired))
                        {
                            owner.mState = CameraDroid.STATE_WAITING_NON_PRECAPTURE;
                        }
                        break;
                    }
                case CameraDroid.STATE_WAITING_NON_PRECAPTURE:
                    {
                        Debug.WriteLine("CameraDroid.STATE_WAITING_NON_PRECAPTURE");
                        // ControlAeState can be null on some devices
                        Integer aeState = (Integer)result.Get(CaptureResult.ControlAeState);
                        if (aeState == null || aeState.IntValue() != ((int)ControlAEState.Precapture))
                        {
                            owner.mState = CameraDroid.STATE_PICTURE_TAKEN;
                            owner.TakePhoto();
                        }
                        break;
                    }
            }
        }
    }
}
