// Builds a complete playable scene from code, so the loop can be run without anyone assembling a
// hierarchy by hand and without a scene file becoming a merge conflict every time someone nudges a
// transform. A scene built by a script is also a scene that can be rebuilt: if the layout is wrong,
// the fix is a line here rather than an afternoon of dragging.
//
// Two things this deliberately does not do.
//
// It does not add the scene to EditorBuildSettings. That file is shared, and when a scene enters the
// build is somebody's decision, not a side effect of pressing a menu item.
//
// It does not add a Directional Light or a Global Volume. The 3D template put both in SampleScene and
// they did nothing there either; under Renderer2D they are pure cost. Sprites here are unlit.
//
// One note on loading the background art. Those textures import as spriteMode 2 (Multiple), which is
// the setting that silenced the jetpack flame for a week, because a Multiple texture's sprites are
// sub-assets rather than the main asset. LoadAssetAtPath<Sprite> returns null for them. They are
// however each sliced into exactly one full-texture sub-sprite, so LoadAllAssetsAtPath finds real,
// serializable sprite assets. That is why the loader below goes through LoadAllAssetsAtPath rather
// than the obvious call, and why nothing here rewrites anyone's import settings.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Player;
using Game.Run;
using Game.World;

namespace Game.EditorTools
{
    /// <summary>
    /// Creates <c>Assets/Scenes/Playable.unity</c> with every system wired.
    /// </summary>
    public static class PlayableSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Playable.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        private const string BackgroundFolder = "Assets/Art/Backgrounds";
        private const string HazardFolder = "Assets/Art/Hazards";
        private const string CoinFolder = "Assets/Art/Coins";
        private const string PowerUpFolder = "Assets/Art/PowerUps";

        /// <summary>One obstacle kind for the director's variant table.</summary>
        private readonly struct ObstacleDef
        {
            public readonly string Name;
            public readonly string Sprite;
            public readonly Vector2 Scale;
            public readonly float Weight;
            public readonly ObstacleDirector.ColliderShape Shape;
            public readonly float ColliderScale;
            public readonly float Spin;

            public ObstacleDef(string name, string sprite, Vector2 scale, float weight,
                ObstacleDirector.ColliderShape shape, float colliderScale, float spin)
            {
                Name = name;
                Sprite = sprite;
                Scale = scale;
                Weight = weight;
                Shape = shape;
                ColliderScale = colliderScale;
                Spin = spin;
            }
        }

        // Four visual kinds across six entries. Sizes are from measured pixel dimensions at 100 pixels
        // per unit, and every one covers a useful share of the 2.67 m band, because a hazard the player
        // can drift past is not an obstacle.
        //
        //   Saw          1.28 x 1.28   round, spins, unmistakably a threat
        //   Zapper       0.22 x 1.73   the genre's signature hazard, a thin vertical beam
        //   Spike up     0.77 x 1.31   spike_top points up, narrow end at the top of the image
        //   Spike down   0.77 x 1.31   spike_bottom is its mirror
        //
        // The two saw frames differ only in artwork and spin, so they read as one kind with variation.
        // The beams are scaled well past their authored size, which softens them - acceptable for a
        // glowing beam and not for anything with an edge, which is why nothing else is stretched.
        //
        // Everything stays centred on its band. Anchoring a hazard to a band edge would leave the band
        // centre clear, and the centre is exactly where PlayerReach models the player travelling, so
        // the generator's promise that a blocked band cannot be passed would become false.
        private static readonly ObstacleDef[] Obstacles =
        {
            new ObstacleDef("Saw",        "saw",           Vector2.one,             0.30f,
                ObstacleDirector.ColliderShape.Circle, 0.90f,  200f),
            new ObstacleDef("SawFast",    "saw_move",      Vector2.one,             0.15f,
                ObstacleDirector.ColliderShape.Circle, 0.90f, -340f),
            new ObstacleDef("Zapper",     "laserRed01",    new Vector2(2.4f, 3.2f), 0.25f,
                ObstacleDirector.ColliderShape.Box,    0.90f,    0f),
            new ObstacleDef("ZapperAlt",  "laserGreen02",  new Vector2(1.7f, 2.4f), 0.10f,
                ObstacleDirector.ColliderShape.Box,    0.90f,    0f),
            new ObstacleDef("SpikeUp",    "spike_top",     new Vector2(1.5f, 1.5f), 0.10f,
                ObstacleDirector.ColliderShape.Box,    0.85f,    0f),
            new ObstacleDef("SpikeDown",  "spike_bottom",  new Vector2(1.5f, 1.5f), 0.10f,
                ObstacleDirector.ColliderShape.Box,    0.85f,    0f)
        };

        // Matched to ObstacleDirector's coin tiers by name, so the values and weights stay owned there
        // and are not duplicated here.
        private static readonly (string Tier, string Sprite)[] CoinSprites =
        {
            ("Bronze", "coin_bronze"),
            ("Silver", "coin_silver"),
            ("Gold", "coin_gold")
        };

        private const string ShieldSprite = "powerup_bubble";

        // Stand-in. There is no magnet pictogram in the art set, so this is the blank badge from the
        // same family as the shield, and the director tints it purple to tell them apart.
        private const string MagnetSprite = "powerup_empty";

        // The X the player controller captures as its permanent lock in Awake and never releases.
        // Matches the spawn SampleScene already uses. Left of centre, so obstacles arriving from the
        // right give the player time to read them.
        private const float PlayerSpawnX = -6f;
        private const float PlayerSpawnY = 0f;

        private const float CameraSize = 5f;
        private const float CameraZ = -10f;

        /// <summary>One parallax plane: which sprite, how fast, how high, and how far back.</summary>
        private struct LayerDef
        {
            public string Name;
            public string Sprite;
            public float Parallax;
            public float Y;
            public int SortingOrder;

            public LayerDef(string name, string sprite, float parallax, float y, int sortingOrder)
            {
                Name = name;
                Sprite = sprite;
                Parallax = parallax;
                Y = y;
                SortingOrder = sortingOrder;
            }
        }

        // Parallax rises towards the front. The sky barely drifts, the ground almost keeps pace with
        // the obstacles. All sorting orders are below the flame at -2, so nothing here can draw over
        // the player.
        //
        // Every Y below is derived from the sprite's measured height and the play bounds, not chosen
        // by eye. All these sprites are centre-pivot at 100 pixels per unit with no transparent
        // padding, so the transform Y is the sprite's centre and half its height reaches each edge.
        //
        // GROUND. groundLayer2 is 1024x200, so 2.00 units tall, half-height 1.00. The player's play
        // bounds are -4 to +4 and they can pin against -4, so the ground's top edge belongs on that
        // line rather than floating above it: centre = -4.00 - 1.00 = -5.00. Top edge -4.00, bottom
        // edge -6.00. The screen bottom is -5.00, so exactly 1.00 unit of it is visible, a tenth of
        // the ten-unit view. Its grass tufts occupy the first 40 rows, -4.00 down to -4.40, and a
        // player pinned at the floor has its sprite bottom at -4.55 and draws at order 0 in front of
        // this at -70, so its legs overlap the tufts and it reads as skimming the surface.
        //
        // The previous table used groundLayer1 at -3.4. That is 1024x400, so it spanned -5.4 to -1.4:
        // 3.6 visible units, over a third of the screen, with its top edge reaching towards the
        // middle. That is what read as flying over a lake.
        //
        // SKY. backgroundEmpty rather than backgroundColorGrass. The latter has a hard green horizon
        // baked in at row 638, only 1.26 units below its own centre, so putting that horizon on the
        // mechanical floor forces its centre to -2.74 and leaves the top 2.6 units of screen blank.
        // backgroundEmpty is the same palette as a clean gradient with no baked horizon, which lets
        // the silhouette layers supply all the terrain.
        //
        // HAZE, not clouds. Every cloud sprite in this set is a band of cloud over a solid fill
        // occupying its lower 60% - cloudLayer1/2 are opaque from row 160, cloudLayerB1/B2 from row
        // 140. They are horizon haze banks, not floating clouds. Placed high, cloudLayerB2 drops a
        // 2.6-unit pale slab across the upper screen with a hard edge; placed so the slab clears the
        // top edge, the clouds themselves end up off-screen. So it is used as the haze it was drawn
        // to be, which is also what makes the mountains behind it read as distant. High clouds would
        // need new art or a crop.
        //
        // Note these layers are pale blue and near-white atmospheric silhouettes, not green terrain.
        // The result is hazy depth rather than a landscape, which is what the art supports.
        private static readonly LayerDef[] Layers =
        {
            new LayerDef("Sky",       "backgroundEmpty", 0.05f,  0.00f, -100),
            new LayerDef("Haze",      "cloudLayerB2",    0.20f, -3.00f,  -90),
            new LayerDef("Mountains", "mountains",       0.45f, -3.20f,  -80),
            new LayerDef("Ground",    "groundLayer2",    0.80f, -5.00f,  -70)
        };

        [MenuItem("Tools/Jetpack/Build playable scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[PlayableSceneBuilder] Cancelled, because the open scene was not saved.");
                return;
            }

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                Debug.LogError($"[PlayableSceneBuilder] No player prefab at {PlayerPrefabPath}. Nothing built.");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera camera = BuildCamera();
            GameObject player = BuildPlayer(playerPrefab);
            ObstacleDirector director = BuildWorld(player);
            CoinMagnet magnet = BuildMagnet(player, director);
            RunManager run = BuildRunManager(player, director);
            BackgroundScroller background = BuildBackground(director);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);

            if (!saved)
            {
                Debug.LogError($"[PlayableSceneBuilder] Failed to save the scene to {ScenePath}.");
                return;
            }

            AssetDatabase.Refresh();

            Debug.Log(
                $"[PlayableSceneBuilder] Built {ScenePath}.\n" +
                $"  camera      orthographic size {CameraSize}, static at (0, 0, {CameraZ})\n" +
                $"  player      {playerPrefab.name} at ({PlayerSpawnX}, {PlayerSpawnY}), X locked there\n" +
                $"  world       {director.GetType().Name} profile {director.Profile}, {magnet.GetType().Name} wired\n" +
                $"  run         {run.GetType().Name}, freeze on death and keypress to retry\n" +
                "  condition   RunConfig, keys 1/2/3 pick Constant/Progressive/Aggressive\n" +
                "  data        RunLog writes one CSV row per run to the persistent data path\n" +
                "  overlay     RunTestOverlay shows the active condition; delete it for the real HUD\n" +
                $"  background  {Layers.Length} parallax layers\n" +
                "  NOT added to EditorBuildSettings, by design. Add it yourself when you want it built.");
        }

        private static Camera BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, CameraZ);

            Camera camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.20f, 0.28f, 1f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;

            // Without a listener in the scene every AudioSource is silent, and the player's audio
            // director creates its sources at runtime, so there would be nothing obvious to blame.
            go.AddComponent<AudioListener>();

            // No follow script. The player's X never changes, so a camera that follows it would be a
            // component whose Update does nothing, and a standing invitation for the next person to
            // assume the camera is managed and lose an hour finding out it is not.
            return camera;
        }

        private static GameObject BuildPlayer(GameObject prefab)
        {
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.transform.position = new Vector3(PlayerSpawnX, PlayerSpawnY, 0f);
            return player;
        }

        private static ObstacleDirector BuildWorld(GameObject player)
        {
            var go = new GameObject("World");
            ObstacleDirector director = go.AddComponent<ObstacleDirector>();
            SetReference(director, "_player", player.GetComponent<PlayerController>());
            AssignWorldSprites(director);
            return director;
        }

        /// <summary>
        /// Fills the director's sprite slots so the world spawns real art rather than the flat coloured
        /// rectangles it falls back to. The prefab fields are left empty on purpose: they take priority
        /// over these, so authored prefabs will override this without anyone editing code.
        /// </summary>
        private static void AssignWorldSprites(ObstacleDirector director)
        {
            var so = new SerializedObject(director);

            SerializedProperty obstacles = so.FindProperty("_obstacleVariants");
            if (obstacles != null)
            {
                obstacles.arraySize = Obstacles.Length;

                for (int i = 0; i < Obstacles.Length; i++)
                {
                    ObstacleDef def = Obstacles[i];
                    SerializedProperty element = obstacles.GetArrayElementAtIndex(i);

                    Sprite sprite = LoadSprite($"{HazardFolder}/{def.Sprite}.png");
                    if (sprite == null)
                        Debug.LogError(
                            $"[PlayableSceneBuilder] Could not load {def.Sprite}.png, so the " +
                            $"'{def.Name}' obstacle will spawn as a flat rectangle.");

                    element.FindPropertyRelative("Name").stringValue = def.Name;
                    element.FindPropertyRelative("Sprite").objectReferenceValue = sprite;
                    element.FindPropertyRelative("Scale").vector2Value = def.Scale;
                    element.FindPropertyRelative("Weight").floatValue = def.Weight;
                    element.FindPropertyRelative("Collider").enumValueIndex = (int)def.Shape;
                    element.FindPropertyRelative("ColliderScale").floatValue = def.ColliderScale;
                    element.FindPropertyRelative("SpinDegreesPerSecond").floatValue = def.Spin;
                }
            }

            SerializedProperty shield = so.FindProperty("_shieldSprite");
            if (shield != null)
                shield.objectReferenceValue = LoadSprite($"{PowerUpFolder}/{ShieldSprite}.png");

            SerializedProperty magnet = so.FindProperty("_magnetSprite");
            if (magnet != null)
                magnet.objectReferenceValue = LoadSprite($"{PowerUpFolder}/{MagnetSprite}.png");

            // Matched by tier name rather than by index, so reordering or renaming the tiers in the
            // director cannot silently pair a gold value with a bronze sprite.
            SerializedProperty tiers = so.FindProperty("_coinTiers");
            if (tiers != null)
            {
                for (int i = 0; i < tiers.arraySize; i++)
                {
                    SerializedProperty tier = tiers.GetArrayElementAtIndex(i);
                    string name = tier.FindPropertyRelative("Name").stringValue;

                    bool matched = false;
                    foreach ((string tierName, string spriteName) in CoinSprites)
                    {
                        if (tierName != name) continue;

                        tier.FindPropertyRelative("Sprite").objectReferenceValue =
                            LoadSprite($"{CoinFolder}/{spriteName}.png");
                        matched = true;
                        break;
                    }

                    if (!matched)
                        Debug.LogWarning(
                            $"[PlayableSceneBuilder] Coin tier '{name}' has no sprite mapping, so it " +
                            "will spawn as a flat rectangle.");
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static CoinMagnet BuildMagnet(GameObject player, ObstacleDirector director)
        {
            // On the same object as the director, because they are the same system: one spawns coins,
            // the other moves them.
            CoinMagnet magnet = director.gameObject.AddComponent<CoinMagnet>();
            SetReference(magnet, "_player", player.GetComponent<PlayerController>());
            SetReference(magnet, "_director", director);
            return magnet;
        }

        // Run state, condition selection, data recording and the test readout all live on one object.
        // They are one concern - what is being measured and under which condition - and splitting them
        // across the hierarchy would only make them easier to wire up wrongly.
        private static RunManager BuildRunManager(GameObject player, ObstacleDirector director)
        {
            var go = new GameObject("RunManager");

            RunConfig config = go.AddComponent<RunConfig>();
            SetReference(config, "_director", director);

            RunLog log = go.AddComponent<RunLog>();

            RunManager run = go.AddComponent<RunManager>();
            SetReference(run, "_player", player.GetComponent<PlayerController>());
            SetReference(run, "_director", director);
            SetReference(run, "_audio", player.GetComponent<PlayerAudioDirector>());
            SetReference(run, "_config", config);
            SetReference(run, "_log", log);

            RunTestOverlay overlay = go.AddComponent<RunTestOverlay>();
            SetReference(overlay, "_run", run);
            SetReference(overlay, "_config", config);
            SetReference(overlay, "_log", log);
            SetReference(overlay, "_director", director);

            return run;
        }

        private static BackgroundScroller BuildBackground(ObstacleDirector director)
        {
            var root = new GameObject("Background");
            BackgroundScroller scroller = root.AddComponent<BackgroundScroller>();
            SetReference(scroller, "_director", director);

            var built = new List<List<Transform>>();
            var parallax = new List<float>();

            foreach (LayerDef def in Layers)
            {
                Sprite sprite = LoadSprite($"{BackgroundFolder}/{def.Sprite}.png");
                if (sprite == null)
                {
                    Debug.LogError(
                        $"[PlayableSceneBuilder] Could not load a sprite from {def.Sprite}.png, so the " +
                        $"'{def.Name}' layer is missing. The scene is still playable without it.");
                    continue;
                }

                var layerRoot = new GameObject(def.Name);
                layerRoot.transform.SetParent(root.transform, false);

                float width = sprite.bounds.size.x;

                // Enough pieces to cover the view plus one spare, so there is always a piece waiting
                // off-screen to the right and the chain never shows a gap mid-wrap.
                int count = Mathf.Max(3, Mathf.CeilToInt(CameraSize * 2f * (16f / 9f) / width) + 1);

                var pieces = new List<Transform>(count);
                for (int i = 0; i < count; i++)
                {
                    var piece = new GameObject($"{def.Name}_{i}");
                    piece.transform.SetParent(layerRoot.transform, false);
                    piece.transform.position = new Vector3((i - 1) * width, def.Y, 0f);

                    SpriteRenderer renderer = piece.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.sortingOrder = def.SortingOrder;

                    pieces.Add(piece.transform);
                }

                built.Add(pieces);
                parallax.Add(def.Parallax);
            }

            ApplyLayers(scroller, built, parallax);
            return scroller;
        }

        // _layers is private, and Layer holds public fields inside a [Serializable] class, so this goes
        // through SerializedProperty rather than reaching for the field. Same reason as SetReference:
        // the components keep their encapsulation and the editor tool does the reaching.
        private static void ApplyLayers(
            BackgroundScroller scroller, List<List<Transform>> pieces, List<float> parallax)
        {
            var so = new SerializedObject(scroller);
            SerializedProperty layers = so.FindProperty("_layers");

            if (layers == null)
            {
                Debug.LogError("[PlayableSceneBuilder] BackgroundScroller has no _layers field to fill.");
                return;
            }

            layers.arraySize = pieces.Count;

            for (int i = 0; i < pieces.Count; i++)
            {
                SerializedProperty element = layers.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Parallax").floatValue = parallax[i];
                element.FindPropertyRelative("WidthOverride").floatValue = 0f;

                SerializedProperty list = element.FindPropertyRelative("Pieces");
                list.arraySize = pieces[i].Count;
                for (int p = 0; p < pieces[i].Count; p++)
                    list.GetArrayElementAtIndex(p).objectReferenceValue = pieces[i][p];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Assigns a private serialized reference. Every component here finds its dependencies in
        /// Awake anyway, but wiring them explicitly means the scene is correct when you look at it in
        /// the Inspector rather than only once it is running.
        /// </summary>
        private static void SetReference(Object target, string field, Object value)
        {
            if (target == null) return;

            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);

            if (property == null)
            {
                Debug.LogWarning(
                    $"[PlayableSceneBuilder] {target.GetType().Name} has no serialized field '{field}'. " +
                    "It will fall back to finding its own reference at runtime.");
                return;
            }

            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Loads the first sprite at a path, whether the texture imported as Single or as Multiple.
        /// LoadAssetAtPath&lt;Sprite&gt; only works for Single, which is the trap this project has
        /// already lost time to twice.
        /// </summary>
        private static Sprite LoadSprite(string path)
        {
            Sprite direct = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (direct != null) return direct;

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite) return sprite;

            return null;
        }
    }
}
