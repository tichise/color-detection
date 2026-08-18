# Color Detection

カメラ画像などから、**指定した色の範囲にあたる部分**を取り出すための Unity パッケージです。

外部ライブラリにも AR Foundation にも依存しません。`Texture2D` を渡せば動きます。

## 入れかた

`Packages/manifest.json` に追記します。

```json
"tokyo.omicro.color-detection": "file:../../color-detection"
```

テストもプロジェクト側で走らせたい場合は `testables` にも足します。

```json
"testables": [
  "tokyo.omicro.color-detection"
]
```

## 入っているもの

| クラス | 何をするか |
|---|---|
| `BlobDetector` | 色の範囲で画像を塗り分け、つながった一番大きなかたまりとその輪郭を返す |
| `ImageAdjuster` | 画像の明るさと彩度を変える |
| `AdjustAspectRatioOfAspectRatioFitter` | 表示の縦横比を、映している画像に合わせる |

名前空間はすべて `Tichise.OpenCV` です。

---

## BlobDetector

画像の中から「この色の範囲に入っている、つながったかたまり」を探し、**一番大きいものを1つ**返します。
球体や LED のように、まとまった色の面を探す用途を想定しています。

### いちばん短い使い方

```csharp
using Tichise.OpenCV;
using UnityEngine;

// 青色を探す。HSVの目盛りは H:0-179 S:0-255 V:0-255
Vector3 lower = new Vector3(90f, 2.5f, 102f);
Vector3 upper = new Vector3(118f, 255f, 255f);

BlobDetector.Result result =
    BlobDetector.Detect(texture, lower, upper, minArea: 100);

if (result.found)
{
    Debug.Log($"中心 {result.center} / 面積 {result.area}");
}
```

### 引数

```csharp
public static Result Detect(
    Texture2D texture,           // 探す対象の画像。nullなら「見つからない」を返す
    Vector3 hsvLower,            // 色の範囲の下限（H:0-179 S:0-255 V:0-255）
    Vector3 hsvUpper,            // 色の範囲の上限
    int minArea,                 // これ未満の画素数のかたまりは無視する
    int step = 1,                // 画素を何個おきに見るか。1で全画素
    bool collectContour = true)  // 輪郭の画素も集めるか
```

**`step`（間引き）について。** 2 にすると縦横とも半分の画素しか見ないので、およそ4倍速くなります。
球のような大きな対象なら 2〜4 で十分です。間引いても、隣り合うサンプルの**間の画素**が同じ色かどうかを
確かめてからつなぐので、離れた2つのかたまりが1つに融合することはありません。

**`collectContour`（輪郭）について。** 輪郭は、プレビューに線を描くためだけに使います。
描かないなら `false` にすると、かたまりをもう一周なぞる手間がまるごと省けます。

### 戻り値

```csharp
public struct Result
{
    public bool found;            // 見つかったか
    public Vector2 center;        // かたまりの中心。左下が原点、単位は画素
    public int area;              // かたまりの画素数
    public Vector2Int[] contour;  // 輪郭の画素。使い回すので中身は次回書き換わる
    public int contourCount;      // contour のうち有効な個数
}
```

`contour` は毎回作り直さず使い回しています。**あとで使うなら値をコピーしてください。**
有効なのは先頭から `contourCount` 個までです。

```csharp
for (int i = 0; i < result.contourCount; i++)
{
    Vector2Int pixel = result.contour[i];
    // ここで線を描くなど
}
```

### 座標の約束

`center` と `contour` は、`Texture2D.GetPixels32()` と同じく **左下が原点**です。
UI（uGUI や UI Toolkit）は左上が原点なので、重ねて描くときは上下を入れ替えてください。

```csharp
int y = texture.height - 1 - Mathf.RoundToInt(result.center.y);
```

### 色の範囲の決まりごと

- 目盛りは H が 0〜179、S と V が 0〜255 です。
- **下限が上限を超えている指定は「何も拾わない」として扱います。**
  たとえば `H: 150 〜 30` のような、色相の輪をまたぐ指定はできません。
  拾いたい色が赤（色相0付近）をまたぐ場合は、範囲を2回に分けて呼んでください。
- 明るさ（V）の下限を低くしすぎると、暗い場所の画素まで入ります。
  白い壁のように「まだらに範囲へ入る」画像では、拾いやすさが跳ね上がります。

### まだらな画像を拾わない仕組み

白い壁のような場所では、範囲に入る画素が**ごま塩状に散らばって**現れます。
画素数だけで比べると、こうした散らばりが本物のかたまりより大きくなってしまいます。

そこで、かたまりの中に**内側の画素**（上下左右のすべてが範囲内である画素）が
1つも無い場合は、かたまりとして採用しません。幅1画素の線やごま塩は、これで落ちます。

### 補助のメソッド

```csharp
// 1画素が範囲内かどうかだけを調べる
bool inside = BlobDetector.InRange(color, lower, upper);

// RGBをHSVへ。目盛りは H:0-179 S:0-255 V:0-255
BlobDetector.RgbToHsv(color, out float h, out float s, out float v);
```

---

## ImageAdjuster

画像の明るさと彩度を変えた**新しい画像**を作ります。元の画像は書き換えません。

```csharp
using Tichise.OpenCV;

// 明るさ0.5（半分の明るさ）、彩度はそのまま
Texture2D adjusted = ImageAdjuster.Adjust(source, brightness: 0.5f,
    saturationOffset: 0f, outTexture: null);
```

### 引数

| 名前 | 意味 |
|---|---|
| `source` | 元の画像。null なら `outTexture` をそのまま返す |
| `brightness` | 明るさの倍率。1.0 で変化なし、0.5 で半分。255 を超える分は 255 で止まる |
| `saturationOffset` | 彩度のずらし量。**0 で変化なし、-255 で完全なグレースケール** |
| `outTexture` | 前回の結果。同じ大きさなら作り直さずに使い回す |

### 使い回しについて

毎フレーム呼ぶ場合は、前回の結果を `outTexture` に渡してください。
同じ大きさなら作り直さないので、画像を毎回捨てて作る無駄がなくなります。

```csharp
// フィールドに持っておく
Texture2D _adjusted;

void Update()
{
    _adjusted = ImageAdjuster.Adjust(source, 0.5f, 0f, _adjusted);
}

void OnDestroy()
{
    // 自分で作ったものは自分で片付ける
    if (_adjusted != null) Destroy(_adjusted);
}
```

**加工が要らないときは呼ばないでください。** 明るさ 1.0・彩度 0 でも画像を1枚作ってしまいます。
呼ぶ側で次のように省くのが確実です。

```csharp
Texture2D target = source;

if (!Mathf.Approximately(brightness, 1f) || !Mathf.Approximately(saturation, 0f))
{
    _adjusted = ImageAdjuster.Adjust(source, brightness, saturation, _adjusted);
    target = _adjusted;
}
```

---

## AdjustAspectRatioOfAspectRatioFitter

uGUI の `AspectRatioFitter` と組み合わせて使います。映している画像の縦横比を表示側へ反映し、
画像の大きさが変わっても表示が引き伸ばされたままにならないようにします。

---

## テスト

`Tests/Editor/` に EditMode のテストがあります。プロジェクトの `testables` にこのパッケージを
足すと、Unity の Test Runner から実行できます。

| ファイル | 何を確かめているか |
|---|---|
| `BlobDetectorTest` | 面積と中心、輪郭、色の範囲の意味、間引きでつなげないこと、まだらを拾わないこと、座標の原点 |
| `ImageAdjusterTest` | 元を書き換えないこと、明るさと彩度、上限で止まること、使い回し |

## ライセンス

LICENSE を参照してください。
