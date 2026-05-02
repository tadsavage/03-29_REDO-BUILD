using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace SaveLoadSystem
{
    public class SaveThumbnailCapture : MonoBehaviour
    {
        private const int THUMB_WIDTH = 320;
        private const int THUMB_HEIGHT = 180;

        public void CaptureThumbnail(string saveFolderPath, string fileName,
                                     Action<Texture2D> onComplete)
        {
            StartCoroutine(CaptureRoutine(saveFolderPath, fileName, onComplete));
        }

        public static Texture2D LoadThumbnailFromDisk(string fullPath)
        {
            if (!File.Exists(fullPath)) return null;

            byte[] pngBytes = File.ReadAllBytes(fullPath);
            Texture2D texture = new Texture2D(THUMB_WIDTH, THUMB_HEIGHT,
                                               TextureFormat.RGB24, false);
            texture.LoadImage(pngBytes);
            return texture;
        }

        private IEnumerator CaptureRoutine(string saveFolderPath,
                                           string fileName,
                                           Action<Texture2D> onComplete)
        {
            yield return new WaitForEndOfFrame();

            // 1. Full-res screenshot
            Texture2D screenCapture = ScreenCapture.CaptureScreenshotAsTexture();

            // 2. Downsample via temporary RT
            RenderTexture resizeRT = RenderTexture.GetTemporary(
                THUMB_WIDTH, THUMB_HEIGHT, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(screenCapture, resizeRT);

            // 3. Read back into thumbnail texture
            Texture2D thumbnail = new Texture2D(THUMB_WIDTH, THUMB_HEIGHT,
                                                TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = resizeRT;
            thumbnail.ReadPixels(new Rect(0, 0, THUMB_WIDTH, THUMB_HEIGHT), 0, 0);
            thumbnail.Apply();

            // 4. Cleanup
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(resizeRT);
            Destroy(screenCapture);

            // 5. Write PNG
            byte[] pngBytes = thumbnail.EncodeToPNG();
            string fullPath = Path.Combine(saveFolderPath, fileName);
            File.WriteAllBytes(fullPath, pngBytes);

            onComplete?.Invoke(thumbnail);
        }
    }
}
