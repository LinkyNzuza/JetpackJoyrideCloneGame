// Loads animation frames from a Resources folder without depending on how the textures were
// imported.
//
// Why this exists. Both effects used Resources.LoadAll<Sprite>, which only returns anything if
// every PNG imported as a Sprite AND produced sprite sub-assets. Our frames sit at spriteMode 2,
// which is Multiple, and Multiple yields no sub-sprites unless a sheet rect table is defined. So
// the textures loaded fine, zero sprites came back, and both effects reported no frames.
//
// I have now been caught twice by asset metadata I cannot see at runtime: first duplicated sprite
// identifiers, and now a sprite mode. So rather than correct the metadata again, this asks for the
// Texture2D and builds the Sprite itself. A texture loads whatever its sprite settings say, which
// removes the entire class of failure instead of the current instance of it.

using System;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Loads an ordered set of frames from a folder under any Resources directory.
    /// </summary>
    public static class SpriteFrameLoader
    {
        /// <summary>Pixels per unit used when building a sprite from a raw texture.</summary>
        private const float PixelsPerUnit = 100f;

        /// <summary>
        /// Returns every frame in <paramref name="resourcePath"/>, sorted by name so numbered files
        /// play in order.
        /// <para>
        /// Prefers real sprite sub-assets when the import produced them, because those honour whatever
        /// pivot and border were authored. Falls back to building sprites from the textures when it did
        /// not, which is the case that was failing.
        /// </para>
        /// </summary>
        /// <param name="resourcePath">Folder under Resources, for example <c>PlayerFX/Jetpack</c>.</param>
        /// <param name="source">Reports which route produced the frames, for logging.</param>
        public static Sprite[] Load(string resourcePath, out string source)
        {
            source = "none";
            if (string.IsNullOrEmpty(resourcePath)) return Array.Empty<Sprite>();

            Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
            if (sprites != null && sprites.Length > 0)
            {
                Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name));
                source = $"{sprites.Length} imported sprite(s)";
                return sprites;
            }

            Texture2D[] textures = Resources.LoadAll<Texture2D>(resourcePath);
            if (textures == null || textures.Length == 0) return Array.Empty<Sprite>();

            Array.Sort(textures, (a, b) => string.CompareOrdinal(a.name, b.name));

            var built = new Sprite[textures.Length];
            for (int i = 0; i < textures.Length; i++)
                built[i] = BuildSprite(textures[i]);

            source = $"{built.Length} sprite(s) built from texture(s), because the import produced none";
            return built;
        }

        /// <summary>
        /// Wraps a whole texture in a sprite, pivoted centre. Named after the texture so ordering and
        /// any later debugging still make sense.
        /// </summary>
        public static Sprite BuildSprite(Texture2D texture)
        {
            if (texture == null) return null;

            var rect = new Rect(0f, 0f, texture.width, texture.height);
            Sprite sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sprite.name = texture.name;
            return sprite;
        }
    }
}
