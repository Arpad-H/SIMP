using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SquirrelAnimator : MonoBehaviour
{
    [System.Serializable]
    public class SpriteAnimation
    {
        [Tooltip("All sprites from the sprite sheet in order")]
        public Sprite[] frames;

        [Tooltip("Seconds between each frame")]
        public float frameInterval = 0.1f;
    }

    [Header("State Source")]
    [Tooltip("Controller whose state drives which animation plays")]
    public SquirrelController controller;

    [Header("Animations")]
    public SpriteAnimation walking = new SpriteAnimation();
    public SpriteAnimation gliding = new SpriteAnimation();

    private SpriteRenderer _renderer;
    private SquirrelState _currentState;
    private int _currentFrame;
    private float _timer;

    void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();

        if (controller == null)
            controller = GetComponentInParent<SquirrelController>();
    }

    void Start()
    {
        _currentState = GetState();
        ResetToFirstFrame();
    }

    void Update()
    {
        SquirrelState state = GetState();

        // Restart the cycle from the first frame whenever the state changes so
        // the new animation doesn't begin partway through.
        if (state != _currentState)
        {
            _currentState = state;
            ResetToFirstFrame();
            return;
        }

        Advance(GetAnimation(_currentState));
    }

    private void Advance(SpriteAnimation animation)
    {
        if (animation == null || animation.frames == null || animation.frames.Length == 0)
            return;

        _timer += Time.deltaTime;
        if (_timer >= animation.frameInterval)
        {
            _timer -= animation.frameInterval;
            _currentFrame = (_currentFrame + 1) % animation.frames.Length;
            _renderer.sprite = animation.frames[_currentFrame];
        }
    }

    private void ResetToFirstFrame()
    {
        _currentFrame = 0;
        _timer = 0f;

        SpriteAnimation animation = GetAnimation(_currentState);
        if (animation != null && animation.frames != null && animation.frames.Length > 0)
            _renderer.sprite = animation.frames[0];
    }

    private SquirrelState GetState()
    {
        return controller != null ? controller.State : SquirrelState.Walking;
    }

    private SpriteAnimation GetAnimation(SquirrelState state)
    {
        return state == SquirrelState.Gliding ? gliding : walking;
    }
}
