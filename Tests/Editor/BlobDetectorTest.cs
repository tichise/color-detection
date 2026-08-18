using NUnit.Framework;
using Tichise.OpenCV;
using UnityEngine;

namespace Tichise.ColorDetection.Tests
{
    /// <summary>
    /// 色のかたまりを見つける処理(BlobDetector)のテスト。
    ///
    /// もとはOpenCVのネイティブ実装で、macOSのEditorでは動かせなかった。
    /// C#だけの実装にしたのでEditorで直接確かめられる。
    /// </summary>
    public class BlobDetectorTest
    {
        // 青色LEDを想定した検出範囲
        static readonly Vector3 Lower = new Vector3(90f, 2.5f, 102f);

        static readonly Vector3 Upper = new Vector3(118f, 255f, 255f);

        // 範囲に入る青
        static readonly Color32 InRangeBlue = new Color32(0, 90, 255, 255);

        // 範囲に入らない黒
        static readonly Color32 OutOfRange = new Color32(0, 0, 0, 255);

        // ------------------------------------------------------------------
        // 基本
        // ------------------------------------------------------------------

        [Test]
        public void 青い四角の面積と中心を返す()
        {
            var texture = MakeTexture(32, 32, new RectInt(10, 10, 4, 4));

            BlobDetector.Result result =
                BlobDetector.Detect(texture, Lower, Upper, minArea: 10, step: 1);

            Assert.That(result.found, Is.True, "青い四角が見つかっていない");
            Assert.That(result.area, Is.EqualTo(16), "面積が画素数と合っていない");
            Assert.That(result.center.x, Is.EqualTo(11.5f).Within(0.01f));
            Assert.That(result.center.y, Is.EqualTo(11.5f).Within(0.01f));

            Object.DestroyImmediate(texture);
        }

        [Test]
        public void 二つある場合は大きいほうを返す()
        {
            var texture = MakeTexture(48, 48,
                new RectInt(2, 2, 3, 3),
                new RectInt(20, 20, 8, 8));

            BlobDetector.Result result =
                BlobDetector.Detect(texture, Lower, Upper, minArea: 5, step: 1);

            Assert.That(result.found, Is.True);
            Assert.That(result.area, Is.EqualTo(64), "小さいほうを選んでいる");
            Assert.That(result.center.x, Is.EqualTo(23.5f).Within(0.01f));

            Object.DestroyImmediate(texture);
        }

        [Test]
        public void 最小面積に満たなければ見つからない()
        {
            var texture = MakeTexture(32, 32, new RectInt(10, 10, 4, 4));

            BlobDetector.Result result =
                BlobDetector.Detect(texture, Lower, Upper, minArea: 50, step: 1);

            Assert.That(result.found, Is.False, "最小面積より小さいのに見つかっている");

            Object.DestroyImmediate(texture);
        }

        [Test]
        public void 範囲外の色は拾わない()
        {
            // 赤しかない画像
            var texture = Fill(32, 32, new Color32(255, 0, 0, 255));

            BlobDetector.Result result =
                BlobDetector.Detect(texture, Lower, Upper, minArea: 1, step: 1);

            Assert.That(result.found, Is.False, "青の範囲なのに赤を拾っている");

            Object.DestroyImmediate(texture);
        }

        [Test]
        public void テクスチャが無ければ見つからないを返す()
        {
            BlobDetector.Result result =
                BlobDetector.Detect(null, Lower, Upper, minArea: 1);

            Assert.That(result.found, Is.False, "テクスチャが無いのに見つかっている");
            Assert.That(result.contourCount, Is.EqualTo(0));
        }

        // ------------------------------------------------------------------
        // 色の範囲の意味
        //
        // 下限が上限を超えている指定は「何も拾わない」。
        // ここが逆になっていると、真っ白な壁まで拾ってしまう
        // ------------------------------------------------------------------

        [Test]
        public void 色相の下限が上限を超えていたら何も拾わない()
        {
            var lower = new Vector3(150f, 0f, 0f);
            var upper = new Vector3(30f, 255f, 255f);

            var red = new Color32(255, 0, 0, 255);
            var green = new Color32(0, 255, 0, 255);

            Assert.That(BlobDetector.InRange(red, lower, upper), Is.False,
                "下限が上限を超えているのに拾っている");
            Assert.That(BlobDetector.InRange(green, lower, upper), Is.False);
        }

        [Test]
        public void 範囲の内と外を色相で分ける()
        {
            Assert.That(BlobDetector.InRange(InRangeBlue, Lower, Upper), Is.True,
                "範囲内の青を拾えていない");
            Assert.That(BlobDetector.InRange(new Color32(255, 0, 0, 255), Lower, Upper),
                Is.False, "範囲外の赤を拾っている");
        }

        [Test]
        public void 明るさが下限に満たない色は拾わない()
        {
            // 色相は範囲内だが、とても暗い青
            var darkBlue = new Color32(0, 8, 20, 255);

            Assert.That(BlobDetector.InRange(darkBlue, Lower, Upper), Is.False,
                "明るさの下限を下回っているのに拾っている");
        }

        [Test]
        public void RGBからHSVへの変換がOpenCVの目盛りになっている()
        {
            // 赤は色相0
            BlobDetector.RgbToHsv(new Color32(255, 0, 0, 255),
                out float h, out float s, out float v);

            Assert.That(h, Is.EqualTo(0f).Within(0.5f), "赤の色相が0でない");
            Assert.That(s, Is.EqualTo(255f).Within(0.5f), "彩度が0-255の目盛りでない");
            Assert.That(v, Is.EqualTo(255f).Within(0.5f), "明度が0-255の目盛りでない");

            // 白は彩度0
            BlobDetector.RgbToHsv(new Color32(255, 255, 255, 255),
                out _, out float whiteS, out float whiteV);

            Assert.That(whiteS, Is.EqualTo(0f).Within(0.5f), "白の彩度が0でない");
            Assert.That(whiteV, Is.EqualTo(255f).Within(0.5f));
        }

        // ------------------------------------------------------------------
        // 輪郭
        //
        // プレビューに輪郭線を描くために、かたまりの「へり」の画素を返す
        // ------------------------------------------------------------------

        [Test]
        public void かたまりのへりの画素を返す()
        {
            // 10x10の四角。へりは外周の1周ぶんで 10*4-4 = 36画素
            var texture = MakeTexture(32, 32, new RectInt(10, 10, 10, 10));

            BlobDetector.Result result =
                BlobDetector.Detect(texture, Lower, Upper, minArea: 1, step: 1);

            Assert.That(result.found, Is.True);
            Assert.That(result.contourCount, Is.EqualTo(36),
                "へりの画素数が外周の数と合っていない");

            Object.DestroyImmediate(texture);
        }

        [Test]
        public void 輪郭には内側の画素を含めない()
        {
            var texture = MakeTexture(32, 32, new RectInt(10, 10, 10, 10));

            BlobDetector.Result result =
                BlobDetector.Detect(texture, Lower, Upper, minArea: 1, step: 1);

            Assert.That(Contains(result, new Vector2Int(14, 14)), Is.False,
                "内側の画素まで輪郭に入っている");
            Assert.That(Contains(result, new Vector2Int(10, 10)), Is.True,
                "角の画素が輪郭に入っていない");

            Object.DestroyImmediate(texture);
        }

        [Test]
        public void 見つからないときは輪郭も空になる()
        {
            var texture = Fill(32, 32, new Color32(255, 0, 0, 255));

            BlobDetector.Result result =
                BlobDetector.Detect(texture, Lower, Upper, minArea: 1, step: 1);

            Assert.That(result.found, Is.False);
            Assert.That(result.contourCount, Is.EqualTo(0), "見つかっていないのに輪郭がある");

            Object.DestroyImmediate(texture);
        }

        [Test]
        public void 輪郭の画素はすべてかたまりの範囲内にある()
        {
            var texture = MakeTexture(32, 32, new RectInt(10, 10, 10, 10));

            BlobDetector.Result result =
                BlobDetector.Detect(texture, Lower, Upper, minArea: 1, step: 1);

            for (int i = 0; i < result.contourCount; i++)
            {
                Vector2Int p = result.contour[i];

                Assert.That(p.x, Is.InRange(10, 19), "輪郭が四角の外へはみ出している");
                Assert.That(p.y, Is.InRange(10, 19), "輪郭が四角の外へはみ出している");
            }

            Object.DestroyImmediate(texture);
        }

        // ------------------------------------------------------------------
        // ノイズ耐性
        //
        // 実機で「真っ白な壁など、無関係な場所を拾う」ことがあった。
        // 白壁は、範囲に入る画素がまだらに散らばる見え方をする
        // ------------------------------------------------------------------

        [Test]
        public void まだらのノイズは大きなかたまりとして拾わない()
        {
            // 2画素おきのまだら。間引き(step=2)だと全サンプルが範囲内に見えるが、
            // 元画像では間の画素が範囲外なので、実際にはつながっていない
            var texture = MakeSpeckle(64, 2);

            BlobDetector.Result result =
                BlobDetector.Detect(texture, Lower, Upper, minArea: 100, step: 2);

            Assert.That(result.found, Is.False,
                "まだらのノイズが1つの大きなかたまりに融合している。"
                + " 白壁のような無関係な場所を拾う原因になる");

            Object.DestroyImmediate(texture);
        }

        [Test]
        public void 間引いても離れたかたまり同士をつなげない()
        {
            // 間を4画素あけて2つの四角を置く。step=4で走査すると、
            // 間の画素を確かめずにつなぐ実装では1つの大きな塊になってしまう
            var texture = MakeTexture(64, 64,
                new RectInt(8, 8, 8, 8),
                new RectInt(40, 8, 8, 8));

            BlobDetector.Result result =
                BlobDetector.Detect(texture, Lower, Upper, minArea: 1, step: 4);

            Assert.That(result.found, Is.True, "どちらの四角も見つかっていない");
            Assert.That(result.center.x, Is.Not.EqualTo(31.5f).Within(3f),
                "2つの四角が1つにつながり、中心が間になっている");

            Object.DestroyImmediate(texture);
        }

        [Test]
        public void 薄い線は拾わない()
        {
            // 幅1画素の横一直線。画素数は最小面積を超えるが、
            // 上下左右がすべて範囲内である「内側」の画素が1つも無い
            var texture = Fill(64, 64, OutOfRange);
            var pixels = texture.GetPixels32();

            for (int i = 5; i < 55; i++)
            {
                pixels[32 * 64 + i] = InRangeBlue;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            BlobDetector.Result result =
                BlobDetector.Detect(texture, Lower, Upper, minArea: 10, step: 1);

            Assert.That(result.found, Is.False,
                "幅1画素の線をかたまりとして拾っている");

            Object.DestroyImmediate(texture);
        }

        [Test]
        public void まだらのノイズよりも塗りつぶされたかたまりを選ぶ()
        {
            // 実機の症状の再現: 画面の大部分がまだらのノイズで、隅に小さな球がある
            var texture = MakeSpeckle(96, 2);
            var pixels = texture.GetPixels32();

            for (int y = 8; y < 20; y++)
            {
                for (int x = 8; x < 20; x++)
                {
                    pixels[y * 96 + x] = InRangeBlue;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            BlobDetector.Result result =
                BlobDetector.Detect(texture, Lower, Upper, minArea: 100, step: 2);

            Assert.That(result.found, Is.True, "球を見つけられていない");
            Assert.That(result.center.x, Is.EqualTo(13.5f).Within(3f),
                "球ではなく、まだらのノイズを選んでいる");
            Assert.That(result.center.y, Is.EqualTo(13.5f).Within(3f),
                "球ではなく、まだらのノイズを選んでいる");

            Object.DestroyImmediate(texture);
        }

        // ------------------------------------------------------------------
        // 座標の約束
        // ------------------------------------------------------------------

        [Test]
        public void 中心の座標は左下を原点として返す()
        {
            // 下寄りに置いた四角。左下原点なら y は小さい値になる
            var texture = MakeTexture(64, 64, new RectInt(28, 4, 8, 8));

            BlobDetector.Result result =
                BlobDetector.Detect(texture, Lower, Upper, minArea: 1, step: 1);

            Assert.That(result.found, Is.True);
            Assert.That(result.center.y, Is.EqualTo(7.5f).Within(0.01f),
                "左下原点になっていない。上下が反転している");

            Object.DestroyImmediate(texture);
        }

        // ------------------------------------------------------------------
        // 補助
        // ------------------------------------------------------------------

        static bool Contains(BlobDetector.Result result, Vector2Int point)
        {
            for (int i = 0; i < result.contourCount; i++)
            {
                if (result.contour[i] == point)
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>背景を範囲外の色で塗り、指定した四角だけ範囲内の青にする</summary>
        static Texture2D MakeTexture(int width, int height, params RectInt[] patches)
        {
            var texture = Fill(width, height, OutOfRange);
            var pixels = texture.GetPixels32();

            foreach (RectInt patch in patches)
            {
                for (int y = patch.yMin; y < patch.yMax; y++)
                {
                    for (int x = patch.xMin; x < patch.xMax; x++)
                    {
                        pixels[y * width + x] = InRangeBlue;
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>spacingおきに1画素だけ範囲内の色を置いた、まだらな画像</summary>
        static Texture2D MakeSpeckle(int size, int spacing)
        {
            var texture = Fill(size, size, OutOfRange);
            var pixels = texture.GetPixels32();

            for (int y = 0; y < size; y += spacing)
            {
                for (int x = 0; x < size; x += spacing)
                {
                    pixels[y * size + x] = InRangeBlue;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
