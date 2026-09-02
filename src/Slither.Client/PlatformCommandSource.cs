using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Slither.Protocol;

namespace Slither.Client;

public sealed class PlatformCommandSource : ICommandSource
{
    private readonly CommandSequence _sequence = new();
    private readonly List<PointerSample> _pointers = [];
    private double _desktopDirectionX = 0;
    private double _desktopDirectionY = 0;
    private bool _desktopBoost = false;

    public PlatformCommandSource(GraphicsDevice graphicsDevice)
    {
        _ = graphicsDevice;
    }

    public VirtualControls VirtualControls { get; } = new();

    public void Update(ScreenLayout layout)
    {
        _pointers.Clear();

#if ANDROID
        foreach (var touch in TouchPanel.GetState())
        {
            _pointers.Add(new PointerSample(
                touch.Id,
                new ScreenPoint(touch.Position.X, touch.Position.Y)));
        }
#else
        var mouse = Mouse.GetState();
        if (mouse.LeftButton == ButtonState.Pressed)
        {
            _pointers.Add(new PointerSample(0, new ScreenPoint(mouse.X, mouse.Y)));
        }

        if (mouse.RightButton == ButtonState.Pressed)
        {
            _pointers.Add(new PointerSample(1, layout.BoostButton.Center));
        }

        var keyboard = Keyboard.GetState();
        _desktopDirectionX =
            (keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right) ? 1 : 0) -
            (keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left) ? 1 : 0);
        _desktopDirectionY =
            (keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up) ? 1 : 0) -
            (keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down) ? 1 : 0);

        var length = Math.Sqrt(
            (_desktopDirectionX * _desktopDirectionX) +
            (_desktopDirectionY * _desktopDirectionY));
        if (length > 1)
        {
            _desktopDirectionX /= length;
            _desktopDirectionY /= length;
        }

        _desktopBoost = keyboard.IsKeyDown(Keys.Space);
#endif

        VirtualControls.Update(layout, _pointers);
    }

    public PlayerCommand SampleCommand()
    {
        var useDesktopDirection =
            (_desktopDirectionX * _desktopDirectionX) +
            (_desktopDirectionY * _desktopDirectionY) > 0;
        var directionX = useDesktopDirection ? _desktopDirectionX : VirtualControls.DirectionX;
        var directionY = useDesktopDirection ? _desktopDirectionY : VirtualControls.DirectionY;
        var hasDirection = useDesktopDirection || VirtualControls.HasDirection;

        return new PlayerCommand(
            _sequence.Next(),
            directionX,
            directionY,
            hasDirection,
            _desktopBoost || VirtualControls.Boost);
    }
}
