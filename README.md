# Color Detection

カメラ画像から、指定した色の範囲にあたる部分を取り出すための部品です。

もとは `tokyo.tichise.opencv` という名前でしたが、OpenCV のライブラリは使っておらず、
中身はすべて C# です。実態に合わせて名前を変えました。

## 入っているもの

| 名前 | 何をするか |
|---|---|
| `BlobDetector` | 色の範囲で塗り分けて、つながった塊とその輪郭を返す |
| `ImageAdjuster` | 画像の明るさと彩度を調整する |
| `AdjustAspectRatioOfAspectRatioFitter` | 表示の縦横比を画像に合わせる |

## 入っていないもの

作品ごとの都合を知っている部品は入れません。たとえば「設定をどのキーで保存しているか」を
知っている処理は、このパッケージではなく作品側に置いてください。

カメラ映像を取り出す処理も入れません。映像の出どころ（ARでもWebカメラでも読み込んだ画像でも）は
使う側の都合なので、`Texture2D` か `Color32[]` にした状態で渡してください。
そのため、このパッケージは AR Foundation にも依存しません。

依存の向きは **作品 → このパッケージ** の一方向だけです。逆向きの参照はしません。

## 使いかた

`Packages/manifest.json` に追記します。

```json
"tokyo.omicro.color-detection": "file:../../color-detection"
```

## ライセンス

LICENSE を参照してください。
