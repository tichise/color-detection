using NUnit.Framework;
using UnityEngine;

namespace Omicro.ColorDetection.Tests
{
    /// <summary>
    /// 明るさと彩度を調整する処理(ImageAdjuster)のテスト。
    ///
    /// 彩度はグレースケールとの混ぜ具合で決まる。
    /// オフセット0で元のまま、-255で完全なグレースケール
    /// </summary>
    public class ImageAdjusterTest
    {
        [Test]
        public void 元の画像は書き換えない()
        {
            var source = Fill(4, 4, new Color32(200, 100, 50, 255));

            Texture2D result = ImageAdjuster.Adjust(source, 0.5f, 0f, null);

            Color32 original = source.GetPixels32()[0];

            Assert.That(original.r, Is.EqualTo(200), "元の画像を書き換えている");
            Assert.That(original.g, Is.EqualTo(100));
            Assert.That(original.b, Is.EqualTo(50));

            Object.DestroyImmediate(source);
            Object.DestroyImmediate(result);
        }

        [Test]
        public void 大きさは変わらない()
        {
            var source = Fill(8, 5, new Color32(120, 120, 120, 255));

            Texture2D result = ImageAdjuster.Adjust(source, 0.5f, 0f, null);

            Assert.That(result.width, Is.EqualTo(8));
            Assert.That(result.height, Is.EqualTo(5));

            Object.DestroyImmediate(source);
            Object.DestroyImmediate(result);
        }

        [Test]
        public void 明るさを下げると暗くなる()
        {
            var source = Fill(4, 4, new Color32(200, 100, 50, 255));

            Texture2D result = ImageAdjuster.Adjust(source, 0.5f, 0f, null);
            Color32 pixel = result.GetPixels32()[0];

            Assert.That(pixel.r, Is.EqualTo(100).Within(1),
                "明るさ0.5で半分になっていない");
            Assert.That(pixel.g, Is.EqualTo(50).Within(1));
            Assert.That(pixel.b, Is.EqualTo(25).Within(1));

            Object.DestroyImmediate(source);
            Object.DestroyImmediate(result);
        }

        [Test]
        public void 明るさ1のままなら色は変わらない()
        {
            var source = Fill(4, 4, new Color32(200, 100, 50, 255));

            Texture2D result = ImageAdjuster.Adjust(source, 1f, 0f, null);
            Color32 pixel = result.GetPixels32()[0];

            Assert.That(pixel.r, Is.EqualTo(200).Within(1));
            Assert.That(pixel.g, Is.EqualTo(100).Within(1));
            Assert.That(pixel.b, Is.EqualTo(50).Within(1));

            Object.DestroyImmediate(source);
            Object.DestroyImmediate(result);
        }

        [Test]
        public void 彩度を最大まで下げるとグレーになる()
        {
            var source = Fill(4, 4, new Color32(200, 100, 50, 255));

            Texture2D result = ImageAdjuster.Adjust(source, 1f, -255f, null);
            Color32 pixel = result.GetPixels32()[0];

            Assert.That(pixel.r, Is.EqualTo(pixel.g).Within(1),
                "グレースケールになっていない");
            Assert.That(pixel.g, Is.EqualTo(pixel.b).Within(1),
                "グレースケールになっていない");

            Object.DestroyImmediate(source);
            Object.DestroyImmediate(result);
        }

        [Test]
        public void 明るさを上げても255を超えない()
        {
            var source = Fill(4, 4, new Color32(250, 250, 250, 255));

            Texture2D result = ImageAdjuster.Adjust(source, 4f, 0f, null);
            Color32 pixel = result.GetPixels32()[0];

            Assert.That(pixel.r, Is.EqualTo(255), "上限を超えて回り込んでいる");
            Assert.That(pixel.g, Is.EqualTo(255));
            Assert.That(pixel.b, Is.EqualTo(255));

            Object.DestroyImmediate(source);
            Object.DestroyImmediate(result);
        }

        [Test]
        public void 透明度はそのまま残る()
        {
            var source = Fill(4, 4, new Color32(200, 100, 50, 128));

            Texture2D result = ImageAdjuster.Adjust(source, 0.5f, 0f, null);
            Color32 pixel = result.GetPixels32()[0];

            Assert.That(pixel.a, Is.EqualTo(128), "透明度が書き換わっている");

            Object.DestroyImmediate(source);
            Object.DestroyImmediate(result);
        }

        [Test]
        public void 同じ大きさなら渡した画像を使い回す()
        {
            var source = Fill(4, 4, new Color32(200, 100, 50, 255));
            var reuse = new Texture2D(4, 4, TextureFormat.RGBA32, false);

            Texture2D result = ImageAdjuster.Adjust(source, 0.5f, 0f, reuse);

            Assert.That(result, Is.SameAs(reuse), "同じ大きさなのに作り直している");

            Object.DestroyImmediate(source);
            Object.DestroyImmediate(reuse);
        }

        [Test]
        public void 元の画像が無ければ渡した画像をそのまま返す()
        {
            var reuse = new Texture2D(4, 4, TextureFormat.RGBA32, false);

            Texture2D result = ImageAdjuster.Adjust(null, 0.5f, 0f, reuse);

            Assert.That(result, Is.SameAs(reuse));

            Object.DestroyImmediate(reuse);
        }

        static Texture2D Fill(int width, int height, Color32 color)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
