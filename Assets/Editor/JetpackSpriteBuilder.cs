// One-shot derivation of the worn-jetpack sprite from a Kenney power-up badge.
//
// Why this exists as an editor tool rather than runtime code. powerup_jetpack.png is a UI badge:
// a solid blue disc (57,157,215) filling all 71x70, with a pure white jetpack pictogram of 342
// pixels in the middle. The pictogram is the only part worth wearing, and cropping alone will not
// free it, because the disc fills the gaps between the two tanks and between the two nozzles. It
// has to be masked.
//
// Doing that mask at runtime would mean Texture2D.GetPixels, which needs Read/Write Enabled in the
// import settings. Import settings have already silenced this project three times: duplicated
// sprite identifiers, spriteMode 2 on the effect frames, and LFS pointer files standing in for
// image data. So the mask happens once, here, and the result is a real PNG on disk that can be
// opened and looked at. Runtime just loads it.
//
// This does not modify powerup_jetpack.png. It reads it and writes somewhere else.
//
// Note on reading the source: the bytes are loaded from disk with File.ReadAllBytes and decoded
// through ImageConversion.LoadImage rather than going through the imported Texture2D asset. A
// texture decoded that way is always readable, whatever the importer decided, which is the same
// class of problem SpriteFrameLoader was written to sidestep.

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds <c>Assets/Resources/PlayerGear/Jetpack/jetpack.png</c> from the white pictogram
    /// inside Kenney's <c>powerup_jetpack.png</c>.
    /// </summary>
    public static class JetpackSpriteBuilder
    {
        private const string SourcePath = "Assets/Art/PowerUps/powerup_jetpack.png";
        private const string OutputFolder = "Assets/Resources/PlayerGear/Jetpack";
        private const string OutputPath = OutputFolder + "/jetpack.png";

        /// <summary>
        /// Lowest value every colour channel must reach for a pixel to count as pictogram.
        /// The pictogram is pure white (255,255,255) and the disc's red channel is 57, so
        /// anything in between is edge antialiasing. Cutting high keeps the silhouette crisp
        /// and guarantees not one disc pixel survives.
        /// </summary>
        private const byte WhiteFloor = 235;

        /// <summary>Lowest alpha a source pixel must have to be considered at all.</summary>
        private const byte AlphaFloor = 200;

        [MenuItem("Tools/Jetpack/Rebuild worn jetpack sprite")]
        public static void Rebuild()
        {
            Texture2D source = LoadSourceTexture();
            if (source == null) return;

            bool[] mask = BuildMask(source, out int keptPixels);

            if (keptPixels == 0)
            {
                Debug.LogError(
                    $"[JetpackSpriteBuilder] No pixel in {SourcePath} reached the white floor of " +
                    $"{WhiteFloor}. Nothing written. Either the source changed or the threshold is wrong.");
                Object.DestroyImmediate(source);
                return;
            }

            if (!TryFindInkBox(mask, source.width, source.height,
                    out int minX, out int maxX, out int minY, out int maxY))
            {
                Object.DestroyImmediate(source);
                return;
            }

            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            int sourceHeight = source.height;

            Texture2D cropped = Crop(source, mask, minX, minY, width, height);
            byte[] png = cropped.EncodeToPNG();

            Object.DestroyImmediate(source);
            Object.DestroyImmediate(cropped);

            if (png == null || png.Length == 0)
            {
                Debug.LogError("[JetpackSpriteBuilder] PNG encoding produced no bytes. Nothing written.");
                return;
            }

            Directory.CreateDirectory(OutputFolder);
            File.WriteAllBytes(OutputPath, png);
            AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);

            // Reported in Unity's bottom-up texture coordinates and in the top-down coordinates an
            // image editor shows, because the two differ by (height - 1 - y) and mixing them up
            // silently crops one row off the wrong end.
            Debug.Log(
                $"[JetpackSpriteBuilder] Wrote {OutputPath} at {width}x{height} from {keptPixels} " +
                $"pictogram pixels. Unity texture rect ({minX}, {minY}, {width}, {height}); the same " +
                $"region top-down is x[{minX}..{maxX}] y[{sourceHeight - 1 - maxY}.." +
                $"{sourceHeight - 1 - minY}].");
        }

        /// <summary>
        /// Decodes the source PNG straight from disk, so the result is readable regardless of
        /// whether the importer enabled Read/Write on the asset.
        /// </summary>
        private static Texture2D LoadSourceTexture()
        {
            string absolute = Path.GetFullPath(SourcePath);

            if (!File.Exists(absolute))
            {
                Debug.LogError($"[JetpackSpriteBuilder] Source not found at {SourcePath}.");
                return null;
            }

            byte[] bytes = File.ReadAllBytes(absolute);

            // A Git LFS pointer is a few hundred bytes of ASCII beginning with "version https://".
            // Catching that here turns an unreadable image into a clear message instead of an
            // empty mask.
            if (bytes.Length < 1024 && bytes.Length > 0 && bytes[0] == (byte)'v')
            {
                Debug.LogError(
                    $"[JetpackSpriteBuilder] {SourcePath} looks like a Git LFS pointer rather than " +
                    "image data. Run 'git lfs pull' and try again.");
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, bytes, markNonReadable: false))
            {
                Debug.LogError($"[JetpackSpriteBuilder] Could not decode {SourcePath} as an image.");
                Object.DestroyImmediate(texture);
                return null;
            }

            return texture;
        }

        /// <summary>
        /// Flags every pixel that belongs to the pictogram. Indexed bottom-up, matching
        /// <see cref="Texture2D.GetPixels32"/>, so the crop below stays in one coordinate system.
        /// </summary>
        private static bool[] BuildMask(Texture2D source, out int keptPixels)
        {
            Color32[] pixels = source.GetPixels32();
            var mask = new bool[pixels.Length];
            keptPixels = 0;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = pixels[i];
                bool isPictogram =
                    c.a >= AlphaFloor &&
                    c.r >= WhiteFloor &&
                    c.g >= WhiteFloor &&
                    c.b >= WhiteFloor;

                mask[i] = isPictogram;
                if (isPictogram) keptPixels++;
            }

            return mask;
        }

        private static bool TryFindInkBox(
            bool[] mask, int width, int height,
            out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = width;
            maxX = -1;
            minY = height;
            maxY = -1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!mask[y * width + x]) continue;

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0 || maxY < 0)
            {
                Debug.LogError("[JetpackSpriteBuilder] Mask was empty, so there is no ink box to crop to.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Copies the masked region into a fresh texture. Kept pixels become opaque white so the
        /// result can be tinted freely at runtime; everything else becomes fully transparent,
        /// including the alpha channel's colour, so no blue bleeds out under filtering.
        /// </summary>
        private static Texture2D Crop(
            Texture2D source, bool[] mask, int originX, int originY, int width, int height)
        {
            int sourceWidth = source.width;
            var output = new Color32[width * height];
            var white = new Color32(255, 255, 255, 255);
            var clear = new Color32(255, 255, 255, 0);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int sourceIndex = (originY + y) * sourceWidth + (originX + x);
                    output[y * width + x] = mask[sourceIndex] ? white : clear;
                }
            }

            var cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
            cropped.SetPixels32(output);
            cropped.Apply();
            return cropped;
        }
    }
}
