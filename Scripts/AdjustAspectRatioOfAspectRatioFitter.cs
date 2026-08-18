using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AdjustAspectRatioOfAspectRatioFitter : MonoBehaviour
{
    private RawImage rawImage;
    private AspectRatioFitter aspectRatioFitter;

    [SerializeField]
    public bool isRotate = true;

    // Start is called before the first frame update
    void Start()
    {
        rawImage = this.gameObject.GetComponent<RawImage>();

        // openCVImageのAspectRatioFitterのaspectRatioを取得する
        aspectRatioFitter = rawImage.GetComponent<AspectRatioFitter>();
    }

    // Update is called once per frame
    void Update()
    {
        if (rawImage.texture == null) {
            return;
        }

        Texture2D texture = (Texture2D)rawImage.texture;

        // rawImageを回転させる
        if (isRotate) {
            ChangeEulerAngle();
        }

        // 渡ってきた画像を元にopenCVImageに追加されてるAspectRatioFitterのaspectRatioを取得する
        aspectRatioFitter.aspectRatio =
            GetAspectRatioOfAspectRatioFitter(texture);
    }

    // rawImageを回転させる
    private void ChangeEulerAngle()
    {
        // 端末の向きに応じて画像回転 ※縦の時は画像ごとの回転が必要
        if (Input.deviceOrientation == DeviceOrientation.LandscapeRight)
        {
            // 電源ボタンが下の場合
            ChangeEulerAngle(0);
        }
        else if (Input.deviceOrientation == DeviceOrientation.LandscapeLeft)
        {
            // 電源ボタンが上の場合
            ChangeEulerAngle(0);
        }
        else if (Input.deviceOrientation == DeviceOrientation.Portrait)
        {
            ChangeEulerAngle(90);
        }
        else if (
            Input.deviceOrientation == DeviceOrientation.PortraitUpsideDown
        )
        {
            ChangeEulerAngle(270);
        }
        else
        {
            // UnityEditorでデバッグ時に表示される
            ChangeEulerAngle(180);
        }
    }

    // rawImageを回転させる
    void ChangeEulerAngle(float z)
    {
        // transformを取得
        Transform myTransform = rawImage.transform;

        // 座標を基準に、回転を取得
        Vector3 angle = myTransform.eulerAngles;
        angle.x = 0.0f; // 座標を基準に、x軸を軸にした回転をz度に変更
        angle.y = 0.0f; // 座標を基準に、y軸を軸にした回転をz度に変更
        angle.z = z; // 座標を基準に、z軸を軸にした回転をz度に変更
        myTransform.eulerAngles = angle; // 回転角度を設定
    }

    // 画像を元にAspectRatioFitterのaspectRatioを取得する
    private static float
    GetAspectRatioOfAspectRatioFitter(Texture2D baseTexture)
    {
        // 現在のテクスチャのアスペクト比を取得して、変更します
        float textureAspectRatioLandscape =
            (baseTexture == null)
                ? 1.0f
                : ((float)baseTexture.width / (float)baseTexture.height);
        float textureAspectRatioPortrait =
            (baseTexture == null)
                ? 1.0f
                : ((float)baseTexture.height / (float)baseTexture.width);

        float aspectRatio;

        /*
    if (Input.deviceOrientation == DeviceOrientation.LandscapeRight)
    {
        // 電源ボタンが下の場合
       aspectRatio = textureAspectRatioLandscape;

    }
    else if (Input.deviceOrientation == DeviceOrientation.LandscapeLeft)
    {
        // 電源ボタンが上の場合
       aspectRatio = textureAspectRatioLandscape;
    }
    else if (Input.deviceOrientation == DeviceOrientation.Portrait)
    {
       aspectRatio = textureAspectRatioPortrait;
    }
    else if (Input.deviceOrientation == DeviceOrientation.PortraitUpsideDown)
    {
       aspectRatio = textureAspectRatioPortrait;
    }
    else
    {
        // UnityEditorでデバッグ時に表示される
       aspectRatio = textureAspectRatioLandscape;
    }
        */

        aspectRatio = textureAspectRatioLandscape;
        

        return aspectRatio;
    }
}
