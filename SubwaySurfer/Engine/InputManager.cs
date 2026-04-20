using System.Windows.Input;

namespace SubwaySurfer.Engine;

public class InputManager
{
    private bool _jumpConsumed;
    private bool _jumpPressed;
    private bool _leftPressed;
    private bool _rightPressed;

    public void KeyDown(Key key)
    {
        if (key is Key.Left or Key.A)  _leftPressed  = true;
        if (key is Key.Right or Key.D) _rightPressed = true;
        if (key is Key.Up or Key.W or Key.Space)
        {
            if (!_jumpConsumed) _jumpPressed = true;
            _jumpConsumed = true;
        }
    }

    public void KeyUp(Key key)
    {
        if (key is Key.Left or Key.A)  _leftPressed  = false;
        if (key is Key.Right or Key.D) _rightPressed = false;
        if (key is Key.Up or Key.W or Key.Space)
        {
            _jumpConsumed = false;
            _jumpPressed  = false;
        }
    }

    // Appelé une fois par frame par le GameEngine
    public bool ConsumeLeft()  { var v = _leftPressed;  _leftPressed  = false; return v; }
    public bool ConsumeRight() { var v = _rightPressed; _rightPressed = false; return v; }
    public bool ConsumeJump()  { var v = _jumpPressed;  _jumpPressed  = false; return v; }
}