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
            Navigation.PopModalAsync();
        }

        void OnCameraClicked(object sender, EventArgs eventArgs)
        {
            cameraPreview.CameraClick.Execute(null);
        }
    }
}
