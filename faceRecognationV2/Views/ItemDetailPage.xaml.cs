using System.ComponentModel;
using Xamarin.Forms;
using faceRecognationV2.ViewModels;

namespace faceRecognationV2.Views
{
    public partial class ItemDetailPage : ContentPage
    {
        public ItemDetailPage()
        {
            InitializeComponent();
            BindingContext = new ItemDetailViewModel();
        }
    }
}
