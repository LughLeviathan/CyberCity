using UnityEngine;
using UnityEngine.InputSystem;

namespace CyberCity
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        public float speed = 4.2f;
        public bool CanMove { get; set; }
        Rigidbody2D body;
        Animator animator;
        SpriteRenderer sprite;
        Vector2 input;
        string animationState;
        string facing = "Down";

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            animator = GetComponent<Animator>();
            sprite = GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            var k = Keyboard.current;
            input = Vector2.zero;
            if (CanMove && k != null)
            {
                input.x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);
                input.y = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0);
                input = input.normalized;
            }
            if (input.sqrMagnitude > 0)
            {
                facing = Mathf.Abs(input.x) > Mathf.Abs(input.y) ? "Side" : input.y > 0 ? "Up" : "Down";
                sprite.flipX = facing == "Side" && input.x < 0;
            }
            string state = (input.sqrMagnitude > 0 ? "Walk" : "Idle") + facing;
            if (animator != null && state != animationState) { animator.Play(state); animationState = state; }
            sprite.sortingOrder = Mathf.RoundToInt(-transform.position.y * 32) + 1;
        }

        void FixedUpdate()
        {
            body.linearVelocity = CanMove ? input * speed : Vector2.zero;
            Vector2 p = body.position;
            Vector2 clamped = WorldLayout.Clamp(p);
            if (clamped != p) body.position = clamped;
        }
    }
}
