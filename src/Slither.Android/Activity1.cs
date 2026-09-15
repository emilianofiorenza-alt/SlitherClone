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

            ApplyImmersiveMode();

            var displaySize = GetLandscapeDisplaySize();
            _game = new SlitherGame(displaySize.Width, displaySize.Height);
            _view = (View)_game.Services.GetService(typeof(View))!;

            SetContentView(_view);
            ApplyImmersiveMode();
            _game.Run();
        }

        protected override void OnResume()
        {
            base.OnResume();
            ApplyImmersiveMode();
        }

        public override void OnWindowFocusChanged(bool hasFocus)
        {
            base.OnWindowFocusChanged(hasFocus);
            if (hasFocus)
            {
                ApplyImmersiveMode();
            }
        }

        private void ApplyImmersiveMode()
        {
            ActionBar?.Hide();
            var window = Window;
            if (window is null)
            {
                return;
            }

            window.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);

#pragma warning disable CA1416, CA1422
            if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
            {
                var attributes = window.Attributes;
                if (attributes is not null)
                {
                    attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
                    window.Attributes = attributes;
                }
            }

            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                window.SetDecorFitsSystemWindows(false);
                var controller = window.InsetsController;
                if (controller is not null)
                {
                    controller.SystemBarsBehavior =
                        (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
                    controller.Hide(WindowInsets.Type.SystemBars());
                }

                return;
            }
#pragma warning restore CA1416, CA1422

#pragma warning disable CS0618
            var decorView = window.DecorView;
            if (decorView is not null)
            {
                decorView.SystemUiVisibility = (StatusBarVisibility)(
                    SystemUiFlags.ImmersiveSticky |
                    SystemUiFlags.Fullscreen |
                    SystemUiFlags.HideNavigation |
                    SystemUiFlags.LayoutStable |
                    SystemUiFlags.LayoutFullscreen |
                    SystemUiFlags.LayoutHideNavigation);
            }
#pragma warning restore CS0618
        }

        private (int Width, int Height) GetLandscapeDisplaySize()
        {
            var width = 0;
            var height = 0;

#pragma warning disable CA1416, CA1422, CS0618
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                var bounds = WindowManager?.CurrentWindowMetrics.Bounds;
                if (bounds is not null)
                {
                    width = bounds.Width();
                    height = bounds.Height();
                }
            }
            else
            {
                var metrics = new global::Android.Util.DisplayMetrics();
                WindowManager?.DefaultDisplay?.GetRealMetrics(metrics);
                width = metrics.WidthPixels;
                height = metrics.HeightPixels;
            }
#pragma warning restore CA1416, CA1422, CS0618

            if (width <= 0 || height <= 0)
            {
                return (1280, 720);
            }

            return (Math.Max(width, height), Math.Min(width, height));
        }
    }
}
