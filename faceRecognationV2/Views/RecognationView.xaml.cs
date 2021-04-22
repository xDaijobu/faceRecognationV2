using System;

using Xamarin.Forms;

namespace faceRecognationV2.Views
{
    public partial class RecognationView : ContentPage
    {
        public RecognationView()
        {
            InitializeComponent();
        }

        void Button_Clicked(object sender, EventArgs eventArgs)
        {
            Shell.Current.Navigation.PopAsync();
        }

        void OnCameraClicked(object sender, EventArgs eventArgs)
        {
            cameraPreview.CameraClick.Execute(null);
            cameraPreview.PictureFinished += new Action(async () =>
            {
                System.Diagnostics.Debug.WriteLine("Fire !");
            });
        }
    }
}
