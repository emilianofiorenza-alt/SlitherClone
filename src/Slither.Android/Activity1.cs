using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;
using Slither.Client;

namespace Slither.Android
{
    [Activity(
        Label = "@string/app_name",
        MainLauncher = true,
        Icon = "@drawable/icon",
        AlwaysRetainTaskState = true,
        LaunchMode = LaunchMode.SingleInstance,
        ScreenOrientation = ScreenOrientation.SensorLandscape,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize
    )]
    public class Activity1 : AndroidGameActivity
    {
        private SlitherGame _game = null!;
        private View _view = null!;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            _game = new SlitherGame();
            _view = (View)_game.Services.GetService(typeof(View))!;

            SetContentView(_view);
            _game.Run();
        }
    }
}
