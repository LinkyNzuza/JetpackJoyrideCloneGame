// Origin: Linky's BackgroundRecycler.cs on Linky's-Branch-. This is a rewrite rather than an edit,
// in my own namespace, so her file stays hers to delete when she chooses.
//
// What her version did: held a flat array of pieces and a camera reference, measured one piece width
// at Start, and each frame recycled any piece for which (camera.x - piece.x) exceeded that width,
// moving it to the right of the rightmost piece.
//
// Why it could not work here. It assumed the camera travels and the world stands still, which is the
// opposite of this game. PlayerController locks the player's X permanently and never releases it, so
// the camera has nothing to follow and never moves. With a static camera and pieces that never move
// either, (camera.x - piece.x) is a constant, so the recycle condition was evaluated every frame and
// could never once become true. The background sat perfectly still while obstacles scrolled past it.
//
// What changed, and why each thing changed:
//
//   Pieces move. They are translated left every frame, because in this game the world moves and the
//   camera does not. This is the actual fix; everything else follows from it.
//
//   Speed is read from ObstacleDirector.EffectiveScrollSpeed rather than being a field. One source of
//   truth for how fast the world is moving, so the background cannot drift out of agreement with the
//   obstacles when the pace curve changes tier. Reading EffectiveScrollSpeed rather than ScrollSpeed
//   also means a frozen world freezes the background for free, instead of this file having to know
//   what death is.
//
//   Recycling tests the piece's own position against a world threshold, not the camera's travel. A
//   piece wraps once its right edge has passed off the left of the screen. The camera is consulted
//   only to ask where the screen edge is, which is a bound, not a movement.
//
//   Layers are a real thing, each with its own pieces and its own parallax factor, so distant layers
//   move slower. Her single array and single width could only ever produce one plane, which is a
//   scrolling image rather than depth.
//
// Kept from her version: the wrap places a recycled piece to the right of whichever piece is
// currently rightmost, rather than jumping it by a fixed multiple of the width. That is the good idea
// in her file. It self-heals an uneven starting layout into an evenly spaced chain, and it does not
// accumulate float error the way repeated fixed offsets would.
//
// Reads the world's speed and writes only its own pieces' X. Never touches the player.

using System;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Scrolls parallax background layers leftward to match the world, recycling each piece once it
    /// leaves the screen.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BackgroundScroller : MonoBehaviour
    {
        /// <summary>
        /// One plane of the background: a chain of pieces that scroll together at their own fraction
        /// of the world speed.
        /// </summary>
        [Serializable]
        public sealed class Layer
        {
            [Tooltip("Pieces in this layer, laid out left to right. Two is the minimum for a seamless " +
                     "chain; three is safer on a wide screen.")]
            public Transform[] Pieces;

            // Tuning: 1 moves with the obstacles, which reads as ground level. Lower values sit further
            // away. Anything at or below 0 is a painted backdrop that never moves.
            [Tooltip("Fraction of the world speed this layer moves at. Lower is further away.")]
            [Range(0f, 1.5f)] public float Parallax = 0.5f;

            [Tooltip("Optional. Width of one piece in metres. Left at zero it is measured from the " +
                     "first piece's renderer, which is usually what you want.")]
            public float WidthOverride;

            [NonSerialized] public float Width;
            [NonSerialized] public bool Warned;
        }

        [Header("Speed source")]
        [Tooltip("Director the world speed is read from. Found automatically if left empty.")]
        [SerializeField] private ObstacleDirector _director;

        // Tuning: only used when there is no director in the scene, so a background scene can be
        // previewed on its own without one.
        [Tooltip("Speed used when no ObstacleDirector exists, in metres per second.")]
        [SerializeField, Range(0f, 30f)] private float _fallbackSpeed = 4f;

        [Header("Layers")]
        [Tooltip("Back to front, or any order; they are independent.")]
        [SerializeField] private Layer[] _layers = Array.Empty<Layer>();

        [Header("Recycling")]
        // Tuning: a piece wraps once its right edge is this far left of the screen edge. Above zero so
        // the wrap happens just off-screen rather than at the exact boundary, where a rounding error
        // would show as a one-frame flicker.
        [Tooltip("Extra metres past the left screen edge before a piece is recycled.")]
        [SerializeField, Range(0f, 20f)] private float _recycleMargin = 1f;

        [Tooltip("Half-width in metres assumed when there is no orthographic camera to measure.")]
        [SerializeField, Range(1f, 60f)] private float _fallbackHalfWidth = 9f;

        private Camera _camera;

        /// <summary>Metres per second the world is moving, before any layer's parallax factor.</summary>
        public float WorldSpeed => _director != null ? _director.EffectiveScrollSpeed : _fallbackSpeed;

        /// <summary>How many pieces were recycled since the scene loaded. Diagnostics only.</summary>
        public int RecycleCount { get; private set; }

        private void Awake()
        {
            if (_director == null) _director = FindFirstObjectByType<ObstacleDirector>();
            _camera = Camera.main;

            if (_director == null)
                Debug.LogWarning(
                    $"[BackgroundScroller] No ObstacleDirector found. Falling back to a fixed " +
                    $"{_fallbackSpeed:0.0} m/s, so the background will not follow the pace curve and " +
                    "will keep moving after the player dies.", this);

            MeasureLayers();
        }

        /// <summary>
        /// Works out each layer's piece width. Called at startup and safe to call again after changing
        /// pieces at runtime.
        /// </summary>
        public void MeasureLayers()
        {
            if (_layers == null) return;

            for (int i = 0; i < _layers.Length; i++)
            {
                Layer layer = _layers[i];
                if (layer == null || layer.Pieces == null || layer.Pieces.Length == 0)
                {
                    Debug.LogWarning($"[BackgroundScroller] Layer {i} has no pieces and will be skipped.", this);
                    continue;
                }

                if (layer.WidthOverride > 0f)
                {
                    layer.Width = layer.WidthOverride;
                    continue;
                }

                layer.Width = MeasureWidth(layer, i);
            }
        }

        // Measured from the renderer's world bounds, so it already accounts for the transform's scale.
        // Her version took the width of piece zero and applied it to every piece; this checks the rest
        // agree, because a chain built from mismatched widths develops a visible gap rather than
        // failing outright, and a silent visual bug is the expensive kind.
        private float MeasureWidth(Layer layer, int index)
        {
            float first = 0f;

            for (int p = 0; p < layer.Pieces.Length; p++)
            {
                Transform piece = layer.Pieces[p];
                if (piece == null) continue;

                var renderer = piece.GetComponent<SpriteRenderer>();
                if (renderer == null) continue;

                float w = renderer.bounds.size.x;
                if (first <= 0f)
                {
                    first = w;
                    continue;
                }

                if (Mathf.Abs(w - first) > first * 0.01f && !layer.Warned)
                {
                    layer.Warned = true;
                    Debug.LogWarning(
                        $"[BackgroundScroller] Layer {index} mixes piece widths ({first:0.00} m and " +
                        $"{w:0.00} m). The chain will develop a gap. Give every piece in a layer the " +
                        "same sprite and scale, or set WidthOverride.", this);
                }
            }

            if (first <= 0f)
            {
                Debug.LogError(
                    $"[BackgroundScroller] Layer {index} has no SpriteRenderer to measure, so its " +
                    "width is unknown and it will not scroll. Set WidthOverride.", this);
            }

            return first;
        }

        private void Update()
        {
            if (_layers == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float world = WorldSpeed;
            float cutoff = LeftEdge() - _recycleMargin;

            for (int i = 0; i < _layers.Length; i++)
            {
                Layer layer = _layers[i];
                if (layer == null || layer.Pieces == null || layer.Width <= 0f) continue;

                float step = world * layer.Parallax * dt;
                ScrollLayer(layer, step, cutoff);
            }
        }

        private void ScrollLayer(Layer layer, float step, float cutoff)
        {
            // Move first, then test. A piece that is moved and then found to be off-screen wraps in the
            // same frame, so it is never drawn in the gap.
            if (step != 0f)
            {
                for (int p = 0; p < layer.Pieces.Length; p++)
                {
                    Transform piece = layer.Pieces[p];
                    if (piece == null) continue;

                    piece.position += Vector3.left * step;
                }
            }

            float half = layer.Width * 0.5f;

            for (int p = 0; p < layer.Pieces.Length; p++)
            {
                Transform piece = layer.Pieces[p];
                if (piece == null) continue;

                // The piece's own right edge, tested against a fixed world threshold. Nothing here asks
                // how far the camera has travelled, which is the assumption that broke the original.
                if (piece.position.x + half >= cutoff) continue;

                float rightmost = RightmostX(layer);
                Vector3 moved = piece.position;
                moved.x = rightmost + layer.Width;
                piece.position = moved;

                RecycleCount++;
            }
        }

        private static float RightmostX(Layer layer)
        {
            float max = float.NegativeInfinity;

            for (int p = 0; p < layer.Pieces.Length; p++)
            {
                Transform piece = layer.Pieces[p];
                if (piece == null) continue;
                if (piece.position.x > max) max = piece.position.x;
            }

            return float.IsNegativeInfinity(max) ? 0f : max;
        }

        // Deliberately no reset on retry. A parallax sky has no start position, so putting the layers
        // back would be a visible jump at the exact moment the player is looking for their new run to
        // begin. The obstacles reset because their layout is the challenge; the background does not
        // because its continuity is the point.

        private float LeftEdge()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return -_fallbackHalfWidth;

            return _camera.orthographic
                ? _camera.transform.position.x - _camera.orthographicSize * _camera.aspect
                : _camera.transform.position.x - _fallbackHalfWidth;
        }
    }
}
