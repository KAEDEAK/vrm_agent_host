using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class LocalImageLoader : MonoBehaviour {
    public Canvas backgroundCanvas; // 背景専用Canvas
    public Image backgroundImage;   // 背景画像を表示するUI Image
    private const string DefaultImageFile = "credits.png"; // 初期背景
    private VRMLoader vrmLoader;
    // 初期背景画像を読み込む
    private void Start() {
        if (backgroundCanvas == null || backgroundImage == null) {
            Debug.LogError(i18nMsg.ERROR_CANVAS_OR_IMAGE_NOT_SET);
            return;
        }

        // CanvasのRenderModeを設定
        if (backgroundCanvas.renderMode != RenderMode.ScreenSpaceCamera) {
            backgroundCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            backgroundCanvas.worldCamera = Camera.main;
            if (backgroundCanvas.worldCamera == null) {
                Debug.LogError(i18nMsg.ERROR_MAIN_CAMERA_NOT_FOUND);
                return;
            }
            Debug.Log(i18nMsg.LOG_CANVAS_RENDERMODE_SET);
        }

        // 初期背景画像を読み込む
        LoadImageToCanvas(UserPaths.GetIMGFilePath(DefaultImageFile));

        // VRMLoader の OnVRMLoadComplete イベントに背景調整を登録
        vrmLoader = GetComponent<VRMLoader>();
        if (vrmLoader != null) {
            vrmLoader.OnVRMLoadComplete += OnVrmModelLoaded;
        }
        else {
            Debug.LogError(i18nMsg.ERROR_VRMLOADER_NOT_ATTACHED);
        }
    }

    private void OnDestroy() {
        if (vrmLoader != null) {
            vrmLoader.OnVRMLoadComplete -= OnVrmModelLoaded;
        }
    }

    /// <summary>
    /// プロジェクトルートからフルパスを生成し、画像ファイルの存在確認を行った上で背景画像を更新する
    /// </summary>
    /// <param name="relativePath">プロジェクトルートからの相対パス</param>
    /// <returns>画像の読み込みに成功したかどうか</returns>
    public bool LoadImageToCanvas(string relativePath) {
        // プロジェクトルートからフルパスを生成
        string projectRootPath = Directory.GetParent(Application.dataPath).FullName;
        string fullPath = Path.Combine(projectRootPath, relativePath);
        fullPath = Path.GetFullPath(fullPath);

        // ファイルが存在しなければエラーを出して処理中止
        if (!File.Exists(fullPath)) {
            Debug.LogError(string.Format(i18nMsg.ERROR_IMAGE_FILE_NOT_FOUND, fullPath));
            return false;
        }

        byte[] imageData = File.ReadAllBytes(fullPath);
        Texture2D texture = new Texture2D(2, 2);
        if (!texture.LoadImage(imageData)) {
            Debug.LogError(string.Format(i18nMsg.ERROR_IMAGE_LOAD_FAILED, fullPath));
            return false;
        }

        if (backgroundImage.sprite != null) {
            var oldSprite = backgroundImage.sprite;
            backgroundImage.sprite = null;
            if (oldSprite.texture != null) Destroy(oldSprite.texture);
            Destroy(oldSprite);
        }

        // 新しい Sprite を生成して背景画像を更新
        Sprite newSprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f)
        );
        backgroundImage.sprite = newSprite;

        // 画像サイズを画面に合わせて調整　// いらない
        //AdjustImageToScreen(texture);

        // 伸縮しない
        DrawImageToScreen(texture);
        AdjustBackground();

        return true;
    }
    public bool LoadTextureAsSprite(Texture2D texture) {
        if (texture == null || backgroundImage == null) return false;
        var sprite = Sprite.Create(texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f));
        backgroundImage.sprite = sprite;
        backgroundImage.color = Color.white;
        return true;
    }
    private void AdjustImageToScreen(Texture2D texture) {
        if (backgroundImage == null || backgroundImage.sprite == null) {
            Debug.LogWarning(i18nMsg.WARNING_IMAGE_OR_SPRITE_NOT_SET);
            return;
        }

        RectTransform bgRectTransform = backgroundImage.GetComponent<RectTransform>();
        RectTransform canvasRect = backgroundCanvas.GetComponent<RectTransform>();
        if (bgRectTransform == null || canvasRect == null) {
            Debug.LogError(i18nMsg.ERROR_RECTTRANSFORM_NOT_FOUND);
            return;
        }

        float screenWidth = canvasRect.rect.width;
        float screenHeight = canvasRect.rect.height;
        float imageAspect = (float)texture.width / texture.height;
        float screenAspect = screenWidth / screenHeight;

        Debug.Log($"sw:{screenWidth}, sh:{screenHeight}, tw:{texture.width}, th:{texture.height}");

        if (imageAspect > screenAspect) {
            bgRectTransform.sizeDelta = new Vector2(screenWidth, screenWidth / imageAspect);
        }
        else {
            bgRectTransform.sizeDelta = new Vector2(screenHeight * imageAspect, screenHeight);
        }

        bgRectTransform.anchorMin = Vector2.zero;
        bgRectTransform.anchorMax = Vector2.one;
        bgRectTransform.anchoredPosition = Vector2.zero;
    }
    private void DrawImageToScreen(Texture2D texture) {
        if (backgroundImage == null || backgroundImage.sprite == null)
            return;

        RectTransform bgRectTransform = backgroundImage.GetComponent<RectTransform>();

        float imageAspect = (float)texture.width / texture.height;

        AspectRatioFitter arFitter = backgroundImage.GetComponent<AspectRatioFitter>();
        if (arFitter != null) {
            arFitter.aspectRatio = imageAspect;
        }
    }
    private void AdjustBackground() {
        if (backgroundImage == null || backgroundImage.sprite == null) {
            Debug.LogWarning(i18nMsg.WARNING_BACKGROUND_IMAGE_NOT_SET);
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null) {
            Debug.LogError(i18nMsg.ERROR_MAIN_CAMERA_NOT_FOUND);
            return;
        }

        Vector3 backgroundPosition = mainCamera.transform.position + mainCamera.transform.forward * (mainCamera.nearClipPlane + 0.1f);
        backgroundCanvas.transform.position = backgroundPosition;
        Debug.Log(string.Format(i18nMsg.LOG_CANVAS_POSITION_ADJUSTED, backgroundCanvas.transform.position));
    }

    public void EnableCanvas(bool enable) {
        if (backgroundCanvas != null) {
            backgroundCanvas.gameObject.SetActive(enable);
            Debug.Log($"🖼️ backgroundCanvas SetActive({enable})");
        }
    }


    private void OnVrmModelLoaded(GameObject vrmModel) {
        Debug.Log(i18nMsg.LOG_VRM_MODEL_LOADED_BACKGROUND);
        if (backgroundImage == null || backgroundImage.sprite == null) {
            Debug.LogWarning(i18nMsg.WARNING_BACKGROUND_IMAGE_NOT_SET);
            return;
        }
        Debug.Log(i18nMsg.LOG_BACKGROUND_IMAGE_VALID);
        AdjustBackground();
    }


}
