using System;
using System.Collections.Generic;
using faceRecognationV2.ViewModels;
using faceRecognationV2.Views;
using Xamarin.Forms;

namespace faceRecognationV2
{
    public partial class AppShell : Xamarin.Forms.Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(ItemDetailPage), typeof(ItemDetailPage));
            Routing.RegisterRoute(nameof(NewItemPage), typeof(NewItemPage));
        }

    }
}
