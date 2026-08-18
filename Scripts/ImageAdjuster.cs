using UnityEngine;

namespace Omicro.ColorDetection
{
    /// <summary>
    /// 画像の明るさと彩度を調整する。OpenCVを使わずC#だけで行う。
    ///
    /// 彩度はグレースケールとの混合で調整する。
    /// オフセット0で元のまま、-255で完全なグレースケールになる。
    /// (旧メニューの彩度スライダーが -255〜0 の範囲だったことに合わせている)
    /// </summary>
    public static class ImageAdjuster
    {
        private static Color32[] s_Buffer;

        /// <summary>
        /// 明るさと彩度を変えた画像を作る。
        /// outTextureに前回の結果を渡すと、同じサイズなら作り直さずに使い回す
        /// </summary>
        public static Texture2D Adjust(Texture2D source, float brightness,
            float saturationOffset, Texture2D outTexture)
        {
            if (source == null)
            {
                return outTexture;
            }

            Color32[] pixels = source.GetPixels32();
            int count = pixels.Length;

            if (s_Buffer == null || s_Buffer.Length != count)
            {
                s_Buffer = new Color32[count];
            }

            // -1(完全なグレー) 〜 0(変化なし)
            float k = Mathf.Clamp(saturationOffset / 255f, -1f, 0f);
            float keep = 1f + k;
            float gray = -k;

            for (int i = 0; i < count; i++)
            {
                Color32 c = pixels[i];

                float r = c.r * brightness;
                float g = c.g * brightness;
                float b = c.b * brightness;

                if (gray > 0f)
                {
                    // 輝度は人の感じ方に合わせた重み付け
                    float lum = 0.299f * r + 0.587f * g + 0.114f * b;
                    r = r * keep + lum * gray;
                    g = g * keep + lum * gray;
                    b = b * keep + lum * gray;
                }

                s_Buffer[i] = new Color32(
                    (byte)Mathf.Clamp(r, 0f, 255f),
                    (byte)Mathf.Clamp(g, 0f, 255f),
                    (byte)Mathf.Clamp(b, 0f, 255f),
                    c.a);
            }

            if (outTexture == null
                || outTexture.width != source.width
                || outTexture.height != source.height)
            {
                if (outTexture != null)
                {
                    Release(outTexture);
                }

                outTexture = new Texture2D(source.width, source.height,
                    TextureFormat.RGBA32, false);
            }

            outTexture.SetPixels32(s_Buffer);
            outTexture.Apply(false, false);
            return outTexture;
        }

        /// <summary>
        /// 使わなくなった画像を捨てる。
        ///
        /// 再生していないEditorでは Destroy が使えず、エラーになる。
        /// Editorの道具やテストからも呼べるように、ここで振り分ける
        /// </summary>
        private static void Release(Texture2D texture)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(texture);
                return;
            }

            Object.DestroyImmediate(texture);
        }
    }
}
