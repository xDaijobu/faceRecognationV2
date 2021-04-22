using System;
using System.Threading.Tasks;
using Xamarin.CommunityToolkit.ObjectModel;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace faceRecognationV2.ViewModels
{
    public class HomePageViewModel : BaseViewModel
    {
        string cameraPermissionStatus;
        public string CameraPermissionStatus
        {
            get => cameraPermissionStatus;
            set => SetProperty(ref cameraPermissionStatus, value);
        }

        public IAsyncCommand RequestCameraPermissionCommand => new AsyncCommand(execute: RequestCameraPermissionAsync);

        public HomePageViewModel()
        {
            Device.BeginInvokeOnMainThread(async () => await CheckAndRequestCameraPermission());
        }

        private async Task CheckAndRequestCameraPermission()
        {
            var permissionStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();

            CameraPermissionStatus = permissionStatus.ToString();

            if (permissionStatus != PermissionStatus.Granted)
                await RequestCameraPermissionAsync();
        }

        private async Task RequestCameraPermissionAsync()
        {
            var result = await Permissions.RequestAsync<Permissions.Camera>();

            CameraPermissionStatus = result.ToString();
        }
    }
}
