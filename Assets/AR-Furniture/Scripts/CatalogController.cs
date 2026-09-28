using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

namespace ARFurniture
{
    public sealed class CatalogController : MonoBehaviour
    {
        private static readonly Color Background = new Color32(244, 241, 235, 255);
        private static readonly Color Surface = new Color32(255, 255, 255, 255);
        private static readonly Color Ink = new Color32(35, 39, 42, 255);
        private static readonly Color Accent = new Color32(31, 111, 91, 255);

        private ProductCatalog _catalog;
        private TMP_FontAsset _font;
        private RectTransform _content;
        private GameObject _catalogPanel;
        private GameObject _productPanel;
        private GameObject _arPanel;
        private ARPlacementController _arPlacement;
        private UnityEngine.UI.Button _arButton;
        private UnityEngine.UI.Image _productImage;
        private TextMeshProUGUI _productName;
        private TextMeshProUGUI _productPrice;
        private TextMeshProUGUI _priceLabel;
        private ProductCategory? _category;
        private int _maximumPrice;

        public Product SelectedProduct { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (SceneManager.GetActiveScene().name != "MainScene" ||
                FindAnyObjectByType<CatalogController>() != null)
            {
                return;
            }

            var catalog = Resources.Load<ProductCatalog>("ProductCatalog");
            if (catalog == null)
            {
                Debug.LogError("ProductCatalog was not found in Resources.");
                return;
            }

            var root = new GameObject(
                "Catalog UI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            root.AddComponent<CatalogController>().Initialize(catalog);
        }

        private void Initialize(ProductCatalog catalog)
        {
            _catalog = catalog;
            _font = TMP_Settings.defaultFontAsset;
            if (_font == null)
            {
                Debug.LogError("TextMeshPro default font asset is missing.");
                return;
            }

            _maximumPrice = MaximumCatalogPrice();

            ConfigureCanvas();
            EnsureEventSystem();
            BuildCatalogPanel();
            BuildProductPanel();
            BuildARPanel();
            InitializeARPlacement();
            RefreshProducts();
        }

        private int MaximumCatalogPrice()
        {
            var maximum = 0;
            foreach (var product in _catalog.products)
            {
                maximum = Mathf.Max(maximum, product.price);
            }

            return maximum;
        }

        private void ConfigureCanvas()
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            }
        }

        private void BuildCatalogPanel()
        {
            _catalogPanel = CreatePanel("Catalog Panel", transform, Background);

            var title = CreateText("Title", _catalogPanel.transform, "Каталог", 54, FontStyles.Bold);
            SetRect(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(36, -112), new Vector2(-36, -24));

            var filters = CreateObject("Category Filters", _catalogPanel.transform);
            SetRect(filters.GetComponent<RectTransform>(), new Vector2(0, 1), Vector2.one, new Vector2(30, -208), new Vector2(-30, -128));
            var filterLayout = filters.AddComponent<HorizontalLayoutGroup>();
            filterLayout.spacing = 12;
            filterLayout.childControlWidth = true;
            filterLayout.childControlHeight = true;
            filterLayout.childForceExpandWidth = true;

            CreateButton(filters.transform, "All Button", "Все", () => SetCategory(null));
            CreateButton(filters.transform, "Sofas Button", "Диваны", () => SetCategory(ProductCategory.Sofas));
            CreateButton(filters.transform, "Armchairs Button", "Кресла", () => SetCategory(ProductCategory.Armchairs));
            CreateButton(filters.transform, "Tables Button", "Столы", () => SetCategory(ProductCategory.Tables));

            _priceLabel = CreateText("Price Label", _catalogPanel.transform, string.Empty, 28);
            SetRect(_priceLabel.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(42, -270), new Vector2(-42, -218));

            var slider = CreateSlider(_catalogPanel.transform);
            SetRect(slider.GetComponent<RectTransform>(), new Vector2(0, 1), Vector2.one, new Vector2(42, -316), new Vector2(-42, -276));
            slider.minValue = 0;
            slider.maxValue = _maximumPrice;
            slider.wholeNumbers = true;
            slider.value = _maximumPrice;
            slider.onValueChanged.AddListener(value =>
            {
                _maximumPrice = Mathf.RoundToInt(value);
                RefreshProducts();
            });

            var scrollView = CreateObject("Product List", _catalogPanel.transform);
            SetRect(scrollView.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(28, 32), new Vector2(-28, -340));
            var scrollRect = scrollView.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            var viewport = CreateObject("Viewport", scrollView.transform);
            SetRect(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var viewportImage = viewport.AddComponent<UnityEngine.UI.Image>();
            viewportImage.color = new Color(1, 1, 1, 0.01f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var content = CreateObject("Content", viewport.transform);
            _content = content.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = Vector2.one;
            _content.pivot = new Vector2(0.5f, 1);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = Vector2.zero;

            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 18;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = _content;
        }

        private void BuildProductPanel()
        {
            _productPanel = CreatePanel("Product Panel", transform, Background);
            _productPanel.SetActive(false);

            var back = CreateButton(_productPanel.transform, "Back Button", "‹ Каталог", ShowCatalog);
            SetRect(back.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -116), new Vector2(290, -36));

            var imageObject = CreateObject("Product Image", _productPanel.transform);
            SetRect(imageObject.GetComponent<RectTransform>(), new Vector2(0, 0.48f), new Vector2(1, 1), new Vector2(40, 0), new Vector2(-40, -148));
            _productImage = imageObject.AddComponent<UnityEngine.UI.Image>();
            _productImage.preserveAspect = true;
            _productImage.raycastTarget = false;

            _productName = CreateText("Product Name", _productPanel.transform, string.Empty, 50, FontStyles.Bold);
            _productName.alignment = TextAlignmentOptions.Center;
            SetRect(_productName.rectTransform, new Vector2(0, 0.36f), new Vector2(1, 0.48f), new Vector2(48, 0), new Vector2(-48, 0));

            _productPrice = CreateText("Product Price", _productPanel.transform, string.Empty, 38);
            _productPrice.alignment = TextAlignmentOptions.Center;
            _productPrice.color = Accent;
            SetRect(_productPrice.rectTransform, new Vector2(0, 0.29f), new Vector2(1, 0.36f), new Vector2(48, 0), new Vector2(-48, 0));

            _arButton = CreateButton(_productPanel.transform, "AR Button", "Посмотреть в AR", ShowAR);
            SetRect(_arButton.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0), new Vector2(48, 174), new Vector2(-48, 274));

            var checkoutButton = CreateButton(_productPanel.transform, "Checkout Button", "Оформить", null);
            SetRect(checkoutButton.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0), new Vector2(48, 50), new Vector2(-48, 150));
            checkoutButton.interactable = false;
        }

        private void BuildARPanel()
        {
            _arPanel = CreatePanel("AR Panel", transform, Color.clear);
            _arPanel.SetActive(false);

            var back = CreateButton(_arPanel.transform, "Back Button", "‹ Товар", ShowProductPanel);
            SetRect(back.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -116), new Vector2(290, -36));
        }

        private void InitializeARPlacement()
        {
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin == null)
            {
                _arButton.interactable = false;
                Debug.LogError("XR Origin was not found in MainScene.");
                return;
            }

            _arPlacement = origin.GetComponent<ARPlacementController>() ??
                origin.gameObject.AddComponent<ARPlacementController>();
            _arPlacement.Initialize();
        }

        private void SetCategory(ProductCategory? category)
        {
            _category = category;
            RefreshProducts();
        }

        private void RefreshProducts()
        {
            _priceLabel.text = $"Цена до: {FormatPrice(_maximumPrice)}";
            for (var i = _content.childCount - 1; i >= 0; i--)
            {
                var card = _content.GetChild(i).gameObject;
                card.SetActive(false);
                Destroy(card);
            }

            foreach (var product in ProductFilter.Apply(_catalog.products, _category, _maximumPrice))
            {
                CreateProductCard(product);
            }

            _content.anchoredPosition = Vector2.zero;
        }

        private void CreateProductCard(Product product)
        {
            var card = CreateObject(product.name, _content);
            var cardImage = card.AddComponent<UnityEngine.UI.Image>();
            cardImage.color = Surface;
            var button = card.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = cardImage;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => ShowProduct(product));
            card.AddComponent<LayoutElement>().preferredHeight = 190;

            var previewObject = CreateObject("Image", card.transform);
            SetRect(previewObject.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0, 1), new Vector2(18, 18), new Vector2(230, -18));
            var preview = previewObject.AddComponent<UnityEngine.UI.Image>();
            preview.sprite = product.image;
            preview.preserveAspect = true;
            preview.raycastTarget = false;

            var name = CreateText("Name", card.transform, product.name, 34, FontStyles.Bold);
            SetRect(name.rectTransform, Vector2.zero, Vector2.one, new Vector2(258, 78), new Vector2(-24, -22));

            var price = CreateText("Price", card.transform, FormatPrice(product.price), 30);
            price.color = Accent;
            SetRect(price.rectTransform, Vector2.zero, Vector2.one, new Vector2(258, 20), new Vector2(-24, -112));
        }

        private void ShowProduct(Product product)
        {
            SelectedProduct = product;
            _productImage.sprite = product.image;
            _productName.text = product.name;
            _productPrice.text = FormatPrice(product.price);
            _catalogPanel.SetActive(false);
            _productPanel.SetActive(true);
        }

        private void ShowCatalog()
        {
            _productPanel.SetActive(false);
            _catalogPanel.SetActive(true);
        }

        private void ShowAR()
        {
            if (_arPlacement == null || SelectedProduct?.prefab == null)
            {
                return;
            }

            _productPanel.SetActive(false);
            _arPanel.SetActive(true);
            _arPlacement.Enter(SelectedProduct.prefab);
        }

        private void ShowProductPanel()
        {
            _arPlacement.Exit();
            _arPanel.SetActive(false);
            _productPanel.SetActive(true);
        }

        private Slider CreateSlider(Transform parent)
        {
            var root = CreateObject("Maximum Price Slider", parent);
            var slider = root.AddComponent<Slider>();

            var background = CreateObject("Background", root.transform);
            SetRect(background.GetComponent<RectTransform>(), new Vector2(0, 0.35f), new Vector2(1, 0.65f), Vector2.zero, Vector2.zero);
            background.AddComponent<UnityEngine.UI.Image>().color = new Color32(201, 204, 200, 255);

            var fillArea = CreateObject("Fill Area", root.transform);
            SetRect(fillArea.GetComponent<RectTransform>(), new Vector2(0, 0.35f), new Vector2(1, 0.65f), new Vector2(10, 0), new Vector2(-10, 0));
            var fill = CreateObject("Fill", fillArea.transform);
            SetRect(fill.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fillImage = fill.AddComponent<UnityEngine.UI.Image>();
            fillImage.color = Accent;

            var handleArea = CreateObject("Handle Slide Area", root.transform);
            SetRect(handleArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-16, 0));
            var handle = CreateObject("Handle", handleArea.transform);
            SetRect(handle.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(-16, -16), new Vector2(16, 16));
            var handleImage = handle.AddComponent<UnityEngine.UI.Image>();
            handleImage.color = Accent;

            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handleImage;
            return slider;
        }

        private GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var panel = CreateObject(name, parent);
            SetRect(panel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var image = panel.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return panel;
        }

        private UnityEngine.UI.Button CreateButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction action)
        {
            var buttonObject = CreateObject(name, parent);
            var image = buttonObject.AddComponent<UnityEngine.UI.Image>();
            image.color = Accent;
            var button = buttonObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            var text = CreateText("Label", buttonObject.transform, label, 26, FontStyles.Bold);
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 4), new Vector2(-8, -4));
            return button;
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, FontStyles style = FontStyles.Normal)
        {
            var textObject = CreateObject(name, parent);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Ink;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static GameObject CreateObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static string FormatPrice(int price)
        {
            return price.ToString("N0", CultureInfo.GetCultureInfo("ru-RU")).Replace('\u00a0', ' ') + " руб.";
        }
    }
}
