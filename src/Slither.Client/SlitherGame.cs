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
    private readonly ISimulationEndpoint _simulation;

    private PlatformCommandSource? _commands;
    private PrimitiveSnapshotRenderer? _renderer;
    private VirtualControlsRenderer? _controlsRenderer;
    private ScreenLayout? _screenLayout;
    private WorldSnapshot _previousSnapshot;
    private WorldSnapshot _snapshot;
    private double _metricsTime;
    private int _renderedFrames;
    private bool _contentLoaded;
#if !ANDROID
    private KeyboardState _previousKeyboard;
#endif

    public SlitherGame(
        int? preferredWidth = null,
        int? preferredHeight = null,
        ISimulationEndpoint? simulationEndpoint = null)
    {
        _simulation = simulationEndpoint ?? new LocalSimulationEndpoint();
#if ANDROID
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = preferredWidth ?? 1280,
            PreferredBackBufferHeight = preferredHeight ?? 720,
            IsFullScreen = true,
            HardwareModeSwitch = false,
            SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight
        };
#else
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight
        };
#endif

        // A stable 60 Hz cadence maps exactly to two refresh intervals on the
        // 120 Hz Android display and avoids the visible judder of an uncapped
        // render rate fluctuating between roughly 60 and 100 FPS.
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
        _graphics.SynchronizeWithVerticalRetrace = true;
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
        _contentLoaded = true;
    }

    protected override void Update(GameTime gameTime)
    {
        using var profile = new ProfileScope("Slither.Update");
#if !ANDROID
        var keyboard = Keyboard.GetState();
        if (keyboard.IsKeyDown(Keys.Escape))
        {
            Exit();
            return;
        }
        if (_simulation is LocalSimulationEndpoint localSimulation)
        {
            if (Pressed(keyboard, Keys.F1)) localSimulation.ConfigurePopulation(PopulationMode.InteractionTest, 10);
            if (Pressed(keyboard, Keys.F2)) localSimulation.ConfigurePopulation(PopulationMode.InteractionTest, 20);
            if (Pressed(keyboard, Keys.F3)) localSimulation.ConfigurePopulation(PopulationMode.StressTest, 50);
            if (Pressed(keyboard, Keys.F4)) localSimulation.ConfigurePopulation(PopulationMode.StressTest, 100);
            if (Pressed(keyboard, Keys.F5)) localSimulation.ConfigurePopulation(PopulationMode.PopulationTest, 20);
            if (Pressed(keyboard, Keys.F6)) localSimulation.ConfigureGameplayMode(GameplayMode.DebugLong);
            if (Pressed(keyboard, Keys.F7)) localSimulation.ConfigureGameplayMode(GameplayMode.Standard);
        }
        _previousKeyboard = keyboard;
#endif

        RefreshScreenLayout();
        _commands!.Update(_screenLayout!);

        _clock.Advance(gameTime.ElapsedGameTime.TotalSeconds, fixedDeltaTime =>
        {
            var command = _commands!.SampleCommand();
            _simulation.Submit(in command);
            _previousSnapshot = _snapshot;
            using (new ProfileScope("Slither.Simulation"))
                _simulation.Step(fixedDeltaTime);
            using (new ProfileScope("Slither.Snapshot"))
                _snapshot = _simulation.CaptureSnapshot();
        });

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        using var profile = new ProfileScope("Slither.Draw");
        _renderer!.UpdateCameraScale(_snapshot.Snake.MatchScore, gameTime.ElapsedGameTime.TotalSeconds);
        _renderer!.Render(_previousSnapshot, _snapshot, _clock.InterpolationAlpha);
        using (new ProfileScope("Slither.Controls"))
        _controlsRenderer!.Render(
            _screenLayout!,
            _commands!.VirtualControls,
            _snapshot.Snake.Size);
        _renderer.RenderScreenFade(_snapshot.ScreenFadeOpacity);
        RecordMetrics(gameTime.ElapsedGameTime.TotalSeconds);
        base.Draw(gameTime);
    }

    protected override void OnActivated(object sender, EventArgs args)
    {
        _clock.Reset();
        if (_contentLoaded)
        {
            RefreshScreenLayout(force: true);
        }
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
        var metrics = _snapshot.Metrics;
        var message = $"Slither Step 3 | {renderHz:F0} FPS | tick {_snapshot.SimulationTick} | " +
                      $"snakes {metrics.AliveSnakes}/{_snapshot.ConfiguredBotCount + 1} visible {_snapshot.VisibleSnakes?.Count ?? 1} | " +
                      $"nodes {metrics.TotalBodyNodes} player {snake.Body.Count} | score {snake.MatchScore} | " +
                      $"dots {_snapshot.VisibleDots.Count} | collisions {metrics.CollisionCandidates}/{metrics.NarrowPhaseTests} | " +
                      $"ms ai {metrics.AiMilliseconds:F2} move {metrics.MotionMilliseconds:F2} " +
                      $"index {metrics.SpatialIndexMilliseconds:F2} collision {metrics.CollisionMilliseconds:F2} " +
                      $"snapshot {metrics.SnapshotMilliseconds:F2} | " +
                      $"deaths {metrics.TotalDeaths} respawns {metrics.TotalRespawns} | " +
                      $"{(snake.IsBoosting ? "BOOST" : $"speed {snake.CurrentSpeed:F0}")}";
        Debug.WriteLine(message);
#if ANDROID
        global::Android.Util.Log.Info("SlitherMetrics", message);
#else
        Window.Title = message;
#endif
        _metricsTime = 0;
        _renderedFrames = 0;
    }

#if !ANDROID
    private bool Pressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
#endif

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
        if (_simulation is LocalSimulationEndpoint localSimulation)
            localSimulation.ConfigureViewport(viewport.Width, viewport.Height);
        _renderer?.SetScreenLayout(_screenLayout);
    }
}
