using NUnit.Framework;
using UnityEngine;

namespace Omicro.ColorDetection.Tests
{
    /// <summary>
    /// 再生中でないと確かめられない部分のテスト。
    ///
    /// 使い終わった画像を捨てる処理は、再生中と停止中で呼ぶものが変わる。
    /// 停止中の側は EditMode のテストで確かめているので、ここでは再生中を見る。
    /// </summary>
    public class ImageAdjusterPlayModeTest
    {
        [Test]
        public void 再生中に大きさが違えば作り直す()
        {
            Assert.That(Application.isPlaying, Is.True, "再生中に実行されていない");

            var source = Fill(8, 8, new Color32(200, 100, 50, 255));
            var reuse = new Texture2D(4, 4, TextureFormat.RGBA32, false);

            Texture2D result = ImageAdjuster.Adjust(source, 0.5f, 0f, reuse);

            Assert.That(result, Is.Not.SameAs(reuse), "大きさが違うのに使い回している");
            Assert.That(result.width, Is.EqualTo(8), "元の画像に合わせていない");
            Assert.That(result.height, Is.EqualTo(8));

            Object.Destroy(source);
            Object.Destroy(result);
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
