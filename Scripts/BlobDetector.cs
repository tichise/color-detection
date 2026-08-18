using System;
using UnityEngine;

namespace Tichise.OpenCV
{
    /// <summary>
    /// 指定した色の範囲に入る画素のかたまり(ブロブ)を1つ見つける。
    ///
    /// OpenCVを使わずC#だけで完結させている。ネイティブライブラリに依存しないので
    /// EditorでもiOSでもAndroidでも同じように動く。
    ///
    /// やっていることは3つだけ:
    ///   1. 画素をHSVへ変換し、指定範囲に入るかを判定する(2値化)
    ///   2. 範囲に入った画素を隣接でつなぎ、かたまりごとに番号を振る(連結成分ラベリング)
    ///   3. いちばん大きいかたまりの画素数(面積)と座標の平均(中心)を返す
    ///
    /// 輪郭を厳密に追跡しなくても、塗りつぶし領域なら
    /// 「画素数 = 面積」「座標の平均 = 重心」で同じ答えが出る。
    /// </summary>
    public static class BlobDetector
    {
        /// <summary>検出結果</summary>
        public struct Result
        {
            /// <summary>見つかったかどうか</summary>
            public bool found;

            /// <summary>かたまりの中心(画像のピクセル座標。左下が原点)</summary>
            public Vector2 center;

            /// <summary>かたまりの画素数</summary>
            public int area;

            /// <summary>かたまりを囲む矩形(左下が原点)</summary>
            public RectInt bounds;

            /// <summary>
            /// かたまりの輪郭にあたる画素(左下が原点)。有効なのは先頭のcontourCount個だけ。
            ///
            /// 中身は使い回しの配列なので、次にDetectを呼ぶと上書きされる。
            /// 保持したい場合は呼び出し側でコピーすること
            /// </summary>
            public Vector2Int[] contour;

            /// <summary>contourのうち有効な個数</summary>
            public int contourCount;
        }

        // 作業用の配列。毎フレーム確保し直さないよう使い回す
        private static int[] s_Labels;
        private static int[] s_Stack;
        private static Vector2Int[] s_Contour;

        /// <summary>
        /// テクスチャから、HSVの範囲に入る最大のかたまりを探す。
        /// </summary>
        /// <param name="texture">読み取り可能なテクスチャ</param>
        /// <param name="hsvLower">HSVの下限。OpenCV系(H:0-179 S:0-255 V:0-255)</param>
        /// <param name="hsvUpper">HSVの上限</param>
        /// <param name="minArea">これ未満の画素数は無視する</param>
        /// <param name="step">画素を間引く量。1で全画素、2で縦横を半分に間引く。大きいほど速いが粗い</param>
        /// <param name="collectContour">
        /// 輪郭の画素も集めるか。輪郭はプレビューに線を描くためだけに使う。
        /// 描かないときは、かたまりをもう一周なぞる手間をまるごと省ける
        /// </param>
        public static Result Detect(Texture2D texture, Vector3 hsvLower, Vector3 hsvUpper,
            int minArea, int step = 1, bool collectContour = true)
        {
            var result = new Result();
            if (texture == null)
            {
                return result;
            }

            step = Mathf.Max(1, step);

            Color32[] pixels = texture.GetPixels32();
            int srcWidth = texture.width;
            int srcHeight = texture.height;

            int width = srcWidth / step;
            int height = srcHeight / step;
            if (width <= 0 || height <= 0)
            {
                return result;
            }

            int count = width * height;
            if (s_Labels == null || s_Labels.Length < count)
            {
                s_Labels = new int[count];
                s_Stack = new int[count];
                s_Contour = new Vector2Int[count];
            }

            // --- 1. 2値化 (範囲内なら-1、範囲外なら0) ---
            for (int y = 0; y < height; y++)
            {
                int srcRow = y * step * srcWidth;
                int dstRow = y * width;
                for (int x = 0; x < width; x++)
                {
                    Color32 c = pixels[srcRow + x * step];
                    s_Labels[dstRow + x] = InRange(c, hsvLower, hsvUpper) ? -1 : 0;
                }
            }

            // --- 2. 連結成分ラベリング (4近傍・スタックで塗りつぶし) ---
            int bestArea = 0;
            long bestSumX = 0;
            long bestSumY = 0;
            int bestMinX = 0, bestMaxX = 0, bestMinY = 0, bestMaxY = 0;
            int bestLabel = 0;
            int label = 0;

            for (int i = 0; i < count; i++)
            {
                if (s_Labels[i] != -1)
                {
                    continue;
                }

                label++;
                int sp = 0;
                s_Stack[sp++] = i;
                s_Labels[i] = label;

                int area = 0;
                int innerArea = 0;
                long sumX = 0;
                long sumY = 0;
                int minX = int.MaxValue, maxX = int.MinValue;
                int minY = int.MaxValue, maxY = int.MinValue;

                while (sp > 0)
                {
                    int p = s_Stack[--sp];
                    int px = p % width;
                    int py = p / width;

                    area++;
                    sumX += px;
                    sumY += py;

                    // 上下左右がすべて範囲内なら「内側」の画素。
                    // 0は範囲外、-1と番号つきは範囲内を意味する
                    if (px > 0 && px < width - 1 && py > 0 && py < height - 1
                        && s_Labels[p - 1] != 0 && s_Labels[p + 1] != 0
                        && s_Labels[p - width] != 0 && s_Labels[p + width] != 0)
                    {
                        innerArea++;
                    }
                    if (px < minX) minX = px;
                    if (px > maxX) maxX = px;
                    if (py < minY) minY = py;
                    if (py > maxY) maxY = py;

                    // 左右上下の隣を見る。
                    //
                    // 間引いている(step>1)ときは、間引き格子で隣どうしでも
                    // 元画像では間に画素が挟まっている。その中間の画素が
                    // 範囲外なら、実際にはつながっていないので繋がない。
                    // これが無いと、白壁のようにぽつぽつと範囲に入る
                    // ノイズの斑点どうしが隙間を飛び越えて融合し、
                    // 1つの巨大なかたまりに育ってしまう
                    if (px > 0 && s_Labels[p - 1] == -1
                        && Bridged(pixels, srcWidth, step, px, py, px - 1, py,
                            hsvLower, hsvUpper))
                    {
                        s_Labels[p - 1] = label;
                        s_Stack[sp++] = p - 1;
                    }
                    if (px < width - 1 && s_Labels[p + 1] == -1
                        && Bridged(pixels, srcWidth, step, px, py, px + 1, py,
                            hsvLower, hsvUpper))
                    {
                        s_Labels[p + 1] = label;
                        s_Stack[sp++] = p + 1;
                    }
                    if (py > 0 && s_Labels[p - width] == -1
                        && Bridged(pixels, srcWidth, step, px, py, px, py - 1,
                            hsvLower, hsvUpper))
                    {
                        s_Labels[p - width] = label;
                        s_Stack[sp++] = p - width;
                    }
                    if (py < height - 1 && s_Labels[p + width] == -1
                        && Bridged(pixels, srcWidth, step, px, py, px, py + 1,
                            hsvLower, hsvUpper))
                    {
                        s_Labels[p + width] = label;
                        s_Stack[sp++] = p + width;
                    }
                }

                // 中身の無い、薄いかたまりは候補にしない。
                //
                // 以前のOpenCV実装は輪郭の多角形の面積で比べていたので、
                // 幅1画素の筋や孤立した点は面積がほぼ0になり、自然と負けていた。
                // 画素数で比べる今の作りにはその働きが無いので、
                // 「内側(上下左右がすべて範囲内)の画素が1つも無いものは
                // 形として薄すぎる」という判定で代える。
                // 塗りつぶした円や四角には必ず内側があり、
                // 白壁のまだらなノイズや細い線には無い
                if (innerArea > 0 && area > bestArea)
                {
                    bestArea = area;
                    bestSumX = sumX;
                    bestSumY = sumY;
                    bestMinX = minX; bestMaxX = maxX;
                    bestMinY = minY; bestMaxY = maxY;
                    bestLabel = label;
                }
            }

            // --- 3. 結果を元の解像度に戻して返す ---
            int realArea = bestArea * step * step;
            if (bestArea == 0 || realArea < minArea)
            {
                return result;
            }

            result.found = true;
            result.area = realArea;
            result.center = new Vector2(
                (float)bestSumX / bestArea * step,
                (float)bestSumY / bestArea * step);
            result.bounds = new RectInt(
                bestMinX * step,
                bestMinY * step,
                (bestMaxX - bestMinX + 1) * step,
                (bestMaxY - bestMinY + 1) * step);

            if (collectContour)
            {
                result.contour = s_Contour;
                result.contourCount = CollectContour(
                    bestLabel, width, height, step,
                    bestMinX, bestMaxX, bestMinY, bestMaxY);
            }

            return result;
        }

        /// <summary>
        /// いちばん大きいかたまりの、へりにあたる画素を集める。
        ///
        /// 判定は単純で、「自分はかたまりの一部だが、上下左右の隣に
        /// かたまりでない画素がある(または画像の端にいる)」ならへり、とする。
        /// OpenCVのFindContoursのように順番に並んだ点列にはならないが、
        /// 輪郭線を描くだけならこれで足りる。
        ///
        /// 探すのは、かたまりを囲む矩形の中だけでよい。外側に画素は無いため
        /// </summary>
        /// <returns>集めた画素の個数</returns>
        private static int CollectContour(int targetLabel, int width, int height,
            int step, int minX, int maxX, int minY, int maxY)
        {
            int found = 0;

            for (int y = minY; y <= maxY; y++)
            {
                int row = y * width;
                for (int x = minX; x <= maxX; x++)
                {
                    int i = row + x;
                    if (s_Labels[i] != targetLabel)
                    {
                        continue;
                    }

                    bool isEdge =
                        x == 0 || y == 0 || x == width - 1 || y == height - 1
                        || s_Labels[i - 1] != targetLabel
                        || s_Labels[i + 1] != targetLabel
                        || s_Labels[i - width] != targetLabel
                        || s_Labels[i + width] != targetLabel;

                    if (isEdge)
                    {
                        // 間引いて調べているので、元の解像度に戻して返す
                        s_Contour[found++] = new Vector2Int(x * step, y * step);
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// 間引き格子で隣どうしの2点が、元画像の上でも本当につながっているか。
        /// 2点の中間にある画素が範囲内なら、つながっているとみなす
        /// </summary>
        static bool Bridged(Color32[] pixels, int srcWidth, int step,
            int ax, int ay, int bx, int by, Vector3 lower, Vector3 upper)
        {
            if (step <= 1)
            {
                // 間引いていなければ、格子の隣は元画像でも隣
                return true;
            }

            int midX = (ax + bx) * step / 2;
            int midY = (ay + by) * step / 2;

            return InRange(pixels[midY * srcWidth + midX], lower, upper);
        }

        /// <summary>
        /// 画素がHSVの範囲に入るか。範囲はOpenCV系(H:0-179 S:0-255 V:0-255)。
        /// 旧OpenCVのInRangeと同じく、下限が上限を超えていれば何も拾わない。
        /// </summary>
        public static bool InRange(Color32 c, Vector3 lower, Vector3 upper)
        {
            RgbToHsv(c, out float h, out float s, out float v);

            // 彩度・明度は素直に範囲判定
            if (s < lower.y || s > upper.y || v < lower.z || v > upper.z)
            {
                return false;
            }

            // 下限が上限を超えている指定は「何も拾わない」。
            //
            // 以前は「0をまたぐ範囲(例: 170〜10は赤)」として全色相近くを
            // 通す解釈にしていたが、これは旧OpenCVのInRangeと正反対の挙動
            // (旧は空集合)。設定画面のスライダー操作で下限>上限が保存されると、
            // 以前は何も検出しなかったのに、今は何でも拾う状態に化けてしまう。
            // 危険な向きに解釈を変えないため、旧の挙動に合わせる
            return h >= lower.x && h <= upper.x;
        }

        /// <summary>
        /// RGBをOpenCV系のHSVへ変換する。H:0-179 S:0-255 V:0-255。
        /// OpenCVのCvtColor(BGR2HSV)と同じ定義に合わせてある。
        /// </summary>
        public static void RgbToHsv(Color32 c, out float h, out float s, out float v)
        {
            float r = c.r;
            float g = c.g;
            float b = c.b;

            float max = Mathf.Max(r, Mathf.Max(g, b));
            float min = Mathf.Min(r, Mathf.Min(g, b));
            float delta = max - min;

            v = max;
            s = max <= 0f ? 0f : delta / max * 255f;

            if (delta <= 0f)
            {
                h = 0f;
                return;
            }

            float hue;
            if (max == r)
            {
                hue = 60f * (g - b) / delta;
            }
            else if (max == g)
            {
                hue = 120f + 60f * (b - r) / delta;
            }
            else
            {
                hue = 240f + 60f * (r - g) / delta;
            }

            if (hue < 0f)
            {
                hue += 360f;
            }

            // OpenCVは色相を0-179で持つ(360度の半分)
            h = hue * 0.5f;
        }
    }
}
