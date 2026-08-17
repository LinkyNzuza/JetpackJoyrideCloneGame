// PlayerAnimation ONLY reads PlayerController.IsAlive and IsThrusting — never raw input or physics.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Drives the Animator by deriving state from <see cref="PlayerController.IsAlive"/> and
    /// <see cref="PlayerController.IsThrusting"/> alone. Never reads input or physics directly.
    /// <para>
    /// States: <c>Flying</c> (alive &amp; thrusting), <c>Falling</c> (alive &amp; not thrusting),
    /// <c>Death</c> (not alive — latches until <see cref="PlayerController.ResetRun"/> restores IsAlive).
    /// </para>
    /// <para>
    /// Same-state derivation issues no Animator call, preventing unwanted clip restarts.
    /// If the Animator component is missing, one error is logged and no further attempts are made.
    /// </para>
    /// </summary>
    public sealed class PlayerAnimation : MonoBehaviour
    {
        // Internal animation state enum — maps 1-to-1 with Animator state names.
        private enum AnimState { Flying, Falling, Death }

        private Animator _animator;
        private PlayerController _controller;

        private AnimState _currentState = AnimState.Falling;
        private bool _deathLatched;
        private bool _animatorMissing;

        // Stable delegates stored as fields so += and -= operate on the exact same instances.
        private Action _handlePlayerDeath;
        private Action _handleRunReset;

        // Missing animator state names already reported, so each logs exactly once.
        private readonly HashSet<string> _reportedMissingStates = new HashSet<string>();

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _controller = GetComponent<PlayerController>();

            if (_animator == null)
            {
                Debug.LogError(
                    "[PlayerAnimation] Animator component not found. Last pose held; no further animation changes.",
                    this);
                _animatorMissing = true;
            }

            // Capture stable delegate references here so OnEnable/OnDisable use the same instances.
            _handlePlayerDeath = HandlePlayerDeath;
            _handleRunReset = HandleRunReset;

            // Present the initial pose so exactly one state is showing from frame one.
            PlayState(_currentState);
        }

        private void OnEnable()
        {
            if (_controller == null) return;
            _controller.OnPlayerDeath += _handlePlayerDeath;
            _controller.OnRunReset += _handleRunReset;
        }

        private void OnDisable()
        {
            if (_controller == null) return;
            _controller.OnPlayerDeath -= _handlePlayerDeath;
            _controller.OnRunReset -= _handleRunReset;
        }

        /// <summary>
        /// Clears the death latch and returns to <c>Falling</c>, the documented initial pose
        /// for a new run. Subscribed to the controller's internal run-reset notification.
        /// </summary>
        private void HandleRunReset()
        {
            _deathLatched = false;
            _currentState = AnimState.Falling;
            if (!_animatorMissing) PlayState(AnimState.Falling);
        }

        /// <summary>
        /// Latches and presents <c>Death</c> immediately. Called directly by
        /// <see cref="PlayerDeath"/> so the pose is correct before any subscriber runs,
        /// rather than depending on event subscription order.
        /// </summary>
        internal void PresentDeath()
        {
            _deathLatched = true;
            if (_currentState == AnimState.Death) return;
            _currentState = AnimState.Death;
            if (!_animatorMissing) PlayState(AnimState.Death);
        }

        private void Update()
        {
            if (_animatorMissing || _controller == null) return;

            // If death was latched but the player is alive again (ResetRun restored IsAlive),
            // clear the latch so the animation returns to normal state derivation.
            if (_deathLatched && _controller.IsAlive)
                _deathLatched = false;

            AnimState desired = DeriveState();
            SetState(desired);
        }

        // ──────────────────────────── State Derivation ─────────────────────────────────

        private AnimState DeriveState()
        {
            if (_deathLatched)
                return AnimState.Death;
            return _controller.IsThrusting ? AnimState.Flying : AnimState.Falling;
        }

        private void SetState(AnimState state)
        {
            // Guard: if already presenting this state, issue no animator call (no clip restart).
            if (state == _currentState) return;
            _currentState = state;
            PlayState(state);
        }

        private void PlayState(AnimState state)
        {
            if (_animatorMissing || _animator == null) return;

            string stateName;
            switch (state)
            {
                case AnimState.Flying:  stateName = "Flying";  break;
                case AnimState.Death:   stateName = "Death";   break;
                default:                stateName = "Falling"; break;
            }

            // Animator.Play on an absent state silently does nothing, which is painful to
            // diagnose. Report each missing name once, then stay quiet.
            if (!_animator.HasState(0, Animator.StringToHash(stateName)))
            {
                if (_reportedMissingStates.Add(stateName))
                    Debug.LogError(
                        $"[PlayerAnimation] Animator has no state named '{stateName}'. " +
                        "Last pose retained.", this);
                return;
            }

            _animator.Play(stateName);
        }

        // ──────────────────────────── Event Handler ────────────────────────────────────

        /// <summary>
        /// Subscribed to <see cref="PlayerController.OnPlayerDeath"/> in OnEnable/OnDisable
        /// via the stable <see cref="_handlePlayerDeath"/> delegate field. Latches the Death
        /// state until <see cref="PlayerController.ResetRun"/> restores <see cref="PlayerController.IsAlive"/>.
        /// </summary>
        private void HandlePlayerDeath()
        {
            _deathLatched = true;
            // Force transition to Death immediately (bypasses the same-state guard intentionally
            // since _currentState was already Death only if previously dead — on fresh death it differs).
            if (_currentState != AnimState.Death)
            {
                _currentState = AnimState.Death;
                if (!_animatorMissing)
                    PlayState(AnimState.Death);
            }
        }
    }
}
