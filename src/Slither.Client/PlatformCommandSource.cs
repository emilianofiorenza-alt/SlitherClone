using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Slither.Protocol;

namespace Slither.Client;

public sealed class PlatformCommandSource : ICommandSource
{
    private readonly CommandSequence _sequence = new();
#if ANDROID
    private readonly GraphicsDevice _graphicsDevice;
#endif

    public PlatformCommandSource(GraphicsDevice graphicsDevice)
    {
#if ANDROID
        _graphicsDevice = graphicsDevice;
#else
        _ = graphicsDevice;
#endif
    }

    public PlayerCommand SampleCommand()
    {
        var direction = Vector2.Zero;
        var boost = false;

#if ANDROID
        var touches = TouchPanel.GetState();
        if (touches.Count > 0)
        {
            var touch = touches[0];
            var center = new Vector2(
                _graphicsDevice.Viewport.Width * 0.5f,
                _graphicsDevice.Viewport.Height * 0.5f);
            direction = touch.Position - center;
            boost = touches.Count > 1;
        }
#else
        var keyboard = Keyboard.GetState();
        direction.X = (keyboard.IsKeyDown(Keys.Right) ? 1 : 0) -
                      (keyboard.IsKeyDown(Keys.Left) ? 1 : 0);
        direction.Y = (keyboard.IsKeyDown(Keys.Up) ? 1 : 0) -
                      (keyboard.IsKeyDown(Keys.Down) ? 1 : 0);
        boost = keyboard.IsKeyDown(Keys.Space);
#endif

        if (direction.LengthSquared() > 1)
        {
            direction.Normalize();
        }

        return new PlayerCommand(_sequence.Next(), direction.X, direction.Y, boost);
    }
}
