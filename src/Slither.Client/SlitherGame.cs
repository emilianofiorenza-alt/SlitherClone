using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Slither.Core;
using Slither.Protocol;

namespace Slither.Client;

public sealed class SlitherGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly FixedStepAccumulator _clock = new();
    private readonly ISimulationEndpoint _simulation = new LocalSimulationEndpoint();

    private PlatformCommandSource? _commands;
    private PrimitiveSnapshotRenderer? _renderer;
    private VirtualControlsRenderer? _controlsRenderer;
    private ScreenLayout? _screenLayout;
    private WorldSnapshot _previousSnapshot;
    private WorldSnapshot _snapshot;
    private double _metricsTime;
    private int _renderedFrames;

    public SlitherGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight
        };

        IsFixedTimeStep = false;
        IsMouseVisible = true;
    }

    protected override void LoadContent()
    {
        _commands = new PlatformCommandSource(GraphicsDevice);
        _renderer = new PrimitiveSnapshotRenderer(GraphicsDevice);
        _controlsRenderer = new VirtualControlsRenderer(GraphicsDevice);
        RefreshScreenLayout(force: true);
        _snapshot = _simulation.CaptureSnapshot();
        _previousSnapshot = _snapshot;
    }

    protected override void Update(GameTime gameTime)
    {
#if !ANDROID
        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
        {
            Exit();
            return;
        }
#endif

        RefreshScreenLayout();
        _commands!.Update(_screenLayout!);

        _clock.Advance(gameTime.ElapsedGameTime.TotalSeconds, fixedDeltaTime =>
        {
            var command = _commands!.SampleCommand();
            _simulation.Submit(in command);
            _previousSnapshot = _snapshot;
            _simulation.Step(fixedDeltaTime);
            _snapshot = _simulation.CaptureSnapshot();
        });

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _renderer!.Render(_previousSnapshot, _snapshot, _clock.InterpolationAlpha);
        _controlsRenderer!.Render(_screenLayout!, _commands!.VirtualControls);
        RecordMetrics(gameTime.ElapsedGameTime.TotalSeconds);
        base.Draw(gameTime);
    }

    protected override void OnActivated(object sender, EventArgs args)
    {
        _clock.Reset();
        RefreshScreenLayout(force: true);
        base.OnActivated(sender, args);
    }

    protected override void OnDeactivated(object sender, EventArgs args)
    {
        _clock.Reset();
        base.OnDeactivated(sender, args);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _renderer?.Dispose();
            _controlsRenderer?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void RecordMetrics(double elapsedSeconds)
    {
        _metricsTime += elapsedSeconds;
        _renderedFrames++;
        if (_metricsTime < 1)
        {
            return;
        }

        var renderHz = _renderedFrames / _metricsTime;
        var snake = _snapshot.Snake;
        var message = $"Slither Step 2C | {renderHz:F0} FPS | tick {_snapshot.SimulationTick} | " +
                      $"pos {snake.HeadX:F1},{snake.HeadY:F1} | body {snake.Body.Count} | " +
                      $"{(snake.IsBoosting ? "BOOST" : $"speed {snake.CurrentSpeed:F0}")}";
        Debug.WriteLine(message);
#if !ANDROID
        Window.Title = message;
#endif
        _metricsTime = 0;
        _renderedFrames = 0;
    }

    private void RefreshScreenLayout(bool force = false)
    {
        var viewport = GraphicsDevice.Viewport;
        if (!force &&
            _screenLayout is not null &&
            _screenLayout.ViewportWidth == viewport.Width &&
            _screenLayout.ViewportHeight == viewport.Height)
        {
            return;
        }

        _screenLayout = ScreenLayout.Create(viewport.Width, viewport.Height, ScreenInsets.None);
        _renderer?.SetScreenLayout(_screenLayout);
    }
}
