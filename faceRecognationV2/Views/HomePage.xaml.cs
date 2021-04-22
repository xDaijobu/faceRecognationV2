using Xamarin.Forms;

namespace faceRecognationV2.Views
{
    public partial class HomePage : ContentPage
    {
        public HomePage()
        {
            InitializeComponent();
        }

        void Button_Clicked(System.Object sender, System.EventArgs e)
        {
            Navigation.PushModalAsync(new RecognationView());
        }
    }
}
