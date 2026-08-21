using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PlayerHUDPrefabBuilder
{
    private const string PrefabPath = "Assets/UI/PlayerHUD.prefab";

    private const string ScenePath =
        "Assets/Scenes/TEST_TEST/TEST_FolderVisibility_Restored.unity";

    private static readonly Color Gold =
        new Color(0.76f, 0.54f, 0.22f, 1f);

    private static readonly Color PaleGold =
        new Color(0.96f, 0.82f, 0.48f, 1f);

    private static readonly Color Obsidian =
        new Color(0.025f, 0.035f, 0.055f, 0.96f);

    [InitializeOnLoadMethod]
    private static void BuildMissingHUDOnEditorLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (
                EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null
            )
            {
                return;
            }

            try
            {
                Rebuild();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        };
    }

    [MenuItem("Tools/HVA/Rebuild Player HUD")]
    public static void Rebuild()
    {
        GameObject prefab = BuildPrefabAsset();

        Scene scene = EditorSceneManager.OpenScene(
            ScenePath,
            OpenSceneMode.Single
        );

        RemoveExistingHUD(scene);

        GameObject hudInstance = (GameObject)PrefabUtility.InstantiatePrefab(
            prefab,
            scene
        );

        hudInstance.name = "Player HUD";

        PlayerHealth player = Object.FindAnyObjectByType<PlayerHealth>();
        PlayerHealthHUD hud = hudInstance.GetComponent<PlayerHealthHUD>();

        if (player == null)
        {
            throw new UnityException(
                "Fant ingen PlayerHealth i " + ScenePath
            );
        }

        hud.SetTarget(player);
        EditorUtility.SetDirty(hud);

        PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Player HUD ble bygget som prefab og koblet til spilleren i " +
            ScenePath
        );
    }

    public static void BuildPrefabOnly()
    {
        BuildPrefabAsset();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Player HUD-prefabet ble bygget.");
    }

    private static GameObject BuildPrefabAsset()
    {
        EnsureFolder("Assets", "UI");

        GameObject temporaryHUD = BuildHUD();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
            temporaryHUD,
            PrefabPath,
            out bool prefabSaved
        );

        Object.DestroyImmediate(temporaryHUD);

        if (!prefabSaved || prefab == null)
        {
            throw new UnityException("Kunne ikke lagre PlayerHUD-prefabet.");
        }
        return prefab;
    }

    private static GameObject BuildHUD()
    {
        GameObject root = new GameObject(
            "Player HUD",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(PlayerHealthHUD)
        );

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.45f;

        CanvasGroup vignette = BuildDamageVignette(root.transform);

        RectTransform panel = CreateRect("Health Panel", root.transform);
        AnchorTopLeft(panel, new Vector2(42f, -42f), new Vector2(500f, 138f));

        CanvasGroup criticalGlow = AddCriticalGlow(panel);

        Image outerFrame = panel.gameObject.AddComponent<Image>();
        outerFrame.color = Gold;
        outerFrame.raycastTarget = false;

        Image innerPanel = CreateStretchImage(
            "Obsidian Inlay",
            panel,
            Obsidian,
            new Vector2(3f, 3f),
            new Vector2(-3f, -3f)
        );

        AddCornerDecoration(innerPanel.rectTransform);
        AddRuneEmblem(innerPanel.rectTransform);

        Text eyebrow = CreateText(
            "Eyebrow",
            innerPanel.rectTransform,
            "KATTENS",
            13,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            new Color(0.76f, 0.68f, 0.52f, 1f)
        );

        AnchorTopLeft(
            eyebrow.rectTransform,
            new Vector2(106f, -13f),
            new Vector2(180f, 19f)
        );

        Text title = CreateText(
            "Title",
            innerPanel.rectTransform,
            "LIVSKRAFT",
            23,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            PaleGold
        );

        AnchorTopLeft(
            title.rectTransform,
            new Vector2(105f, -29f),
            new Vector2(250f, 30f)
        );

        RectTransform barFrame = CreateRect(
            "Segmented Health Bar",
            innerPanel.rectTransform
        );

        AnchorTopLeft(
            barFrame,
            new Vector2(105f, -62f),
            new Vector2(350f, 38f)
        );

        Image barFrameImage = barFrame.gameObject.AddComponent<Image>();
        barFrameImage.color = new Color(0.48f, 0.34f, 0.14f, 1f);
        barFrameImage.raycastTarget = false;

        Image barBackground = CreateStretchImage(
            "Bar Background",
            barFrame,
            new Color(0.055f, 0.065f, 0.08f, 1f),
            new Vector2(3f, 3f),
            new Vector2(-3f, -3f)
        );

        RectTransform barInterior = barBackground.rectTransform;

        RectTransform lagFill = CreateFill(
            "Damage Memory",
            barInterior,
            new Color(1f, 0.75f, 0.16f, 1f),
            out _
        );

        RectTransform healthFill = CreateFill(
            "Current Health",
            barInterior,
            new Color(0.12f, 0.82f, 0.55f, 1f),
            out Image healthFillImage
        );

        CreateStretchImage(
            "Fill Highlight",
            healthFill,
            new Color(1f, 1f, 1f, 0.13f),
            new Vector2(0f, 14f),
            Vector2.zero
        );

        AddSegments(barInterior);

        Text healthValue = CreateText(
            "Health Value",
            barFrame,
            "5  /  5",
            18,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            Color.white
        );

        Stretch(healthValue.rectTransform);

        Text status = CreateText(
            "Health Status",
            innerPanel.rectTransform,
            "",
            14,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            new Color(1f, 0.33f, 0.24f, 1f)
        );

        AnchorTopLeft(
            status.rectTransform,
            new Vector2(106f, -103f),
            new Vector2(350f, 23f)
        );

        PlayerHealthHUD hud = root.GetComponent<PlayerHealthHUD>();
        hud.Configure(
            panel,
            healthFill,
            lagFill,
            healthFillImage,
            healthValue,
            status,
            criticalGlow,
            vignette
        );

        return root;
    }

    private static CanvasGroup BuildDamageVignette(Transform parent)
    {
        RectTransform group = CreateRect("Damage Vignette", parent);
        Stretch(group);

        CanvasGroup canvasGroup = group.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        CreateEdge("Top", group, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -52.5f), new Vector2(0f, 105f));

        CreateEdge("Bottom", group, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 52.5f), new Vector2(0f, 105f));

        CreateEdge("Left", group, new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(52.5f, 0f), new Vector2(105f, 0f));

        CreateEdge("Right", group, new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(-52.5f, 0f), new Vector2(105f, 0f));

        return canvasGroup;
    }

    private static void CreateEdge(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 position,
        Vector2 size
    )
    {
        Image edge = CreateImage(
            name + " Edge",
            parent,
            new Color(0.72f, 0.015f, 0.025f, 0.55f)
        );

        edge.rectTransform.anchorMin = anchorMin;
        edge.rectTransform.anchorMax = anchorMax;
        edge.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        edge.rectTransform.anchoredPosition = position;
        edge.rectTransform.sizeDelta = size;
    }

    private static CanvasGroup AddCriticalGlow(RectTransform panel)
    {
        Image glow = CreateStretchImage(
            "Critical Glow",
            panel,
            new Color(0.85f, 0.02f, 0.035f, 0.8f),
            new Vector2(-9f, -9f),
            new Vector2(9f, 9f)
        );

        glow.transform.SetAsFirstSibling();

        CanvasGroup group = glow.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        return group;
    }

    private static void AddRuneEmblem(RectTransform parent)
    {
        Image diamond = CreateImage("Rune Frame", parent, Gold);
        AnchorTopLeft(
            diamond.rectTransform,
            new Vector2(34f, -35f),
            new Vector2(66f, 66f)
        );
        diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        Image inset = CreateImage(
            "Rune Inlay",
            diamond.transform,
            new Color(0.08f, 0.11f, 0.12f, 1f)
        );
        Stretch(inset.rectTransform, 5f);

        Text rune = CreateText(
            "Life Rune",
            parent,
            "✦",
            38,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            PaleGold
        );

        AnchorTopLeft(
            rune.rectTransform,
            new Vector2(34f, -35f),
            new Vector2(66f, 66f)
        );
    }

    private static void AddCornerDecoration(RectTransform parent)
    {
        Image line = CreateImage(
            "Gold Accent",
            parent,
            new Color(0.76f, 0.54f, 0.22f, 0.65f)
        );

        AnchorTopRight(
            line.rectTransform,
            new Vector2(-20f, -19f),
            new Vector2(78f, 2f)
        );

        Image gem = CreateImage("Corner Rune", parent, Gold);
        AnchorTopRight(
            gem.rectTransform,
            new Vector2(-14f, -14f),
            new Vector2(11f, 11f)
        );
        gem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    private static void AddSegments(RectTransform parent)
    {
        for (int index = 1; index < 5; index++)
        {
            float anchor = index / 5f;
            Image separator = CreateImage(
                "Segment " + index,
                parent,
                new Color(0.02f, 0.025f, 0.035f, 0.82f)
            );

            separator.rectTransform.anchorMin = new Vector2(anchor, 0f);
            separator.rectTransform.anchorMax = new Vector2(anchor, 1f);
            separator.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            separator.rectTransform.sizeDelta = new Vector2(3f, 0f);
            separator.rectTransform.anchoredPosition = Vector2.zero;
        }
    }

    private static RectTransform CreateFill(
        string name,
        Transform parent,
        Color color,
        out Image image
    )
    {
        image = CreateImage(name, parent, color);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static Text CreateText(
        string name,
        Transform parent,
        string value,
        int fontSize,
        FontStyle style,
        TextAnchor alignment,
        Color color
    )
    {
        RectTransform rect = CreateRect(name, parent);
        Text text = rect.gameObject.AddComponent<Text>();

        text.font = Resources.GetBuiltinResource<Font>(
            "LegacyRuntime.ttf"
        );

        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static Image CreateImage(
        string name,
        Transform parent,
        Color color
    )
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Image CreateStretchImage(
        string name,
        Transform parent,
        Color color,
        Vector2 offsetMin,
        Vector2 offsetMax
    )
    {
        Image image = CreateImage(name, parent, color);
        Stretch(image.rectTransform);
        image.rectTransform.offsetMin = offsetMin;
        image.rectTransform.offsetMax = offsetMax;
        return image;
    }

    private static RectTransform CreateRect(
        string name,
        Transform parent
    )
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void AnchorTopLeft(
        RectTransform rect,
        Vector2 position,
        Vector2 size
    )
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void AnchorTopRight(
        RectTransform rect,
        Vector2 position,
        Vector2 size
    )
    {
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void RemoveExistingHUD(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Player HUD")
            {
                Object.DestroyImmediate(root);
            }
        }
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;

        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
