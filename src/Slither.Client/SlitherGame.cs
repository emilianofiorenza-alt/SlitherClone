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

    private ICommandSource? _commands;
    private PrimitiveSnapshotRenderer? _renderer;
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
        _snapshot = _simulation.CaptureSnapshot();
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

        _clock.Advance(gameTime.ElapsedGameTime.TotalSeconds, fixedDeltaTime =>
        {
            var command = _commands!.SampleCommand();
            _simulation.Submit(in command);
            _simulation.Step(fixedDeltaTime);
            _snapshot = _simulation.CaptureSnapshot();
        });

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(10, 15, 24));
        _renderer!.Render(_snapshot, _clock.InterpolationAlpha);
        RecordMetrics(gameTime.ElapsedGameTime.TotalSeconds);
        base.Draw(gameTime);
    }

    protected override void OnActivated(object sender, EventArgs args)
    {
        _clock.Reset();
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
        var message = $"Slither smoke test | render {renderHz:F0} Hz | simulation tick {_snapshot.SimulationTick}";
        Debug.WriteLine(message);
#if !ANDROID
        Window.Title = message;
#endif
        _metricsTime = 0;
        _renderedFrames = 0;
    }
}
