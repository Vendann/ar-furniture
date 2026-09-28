using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

namespace ARFurniture
{
    public static class CheckoutValidation
    {
        public static bool IsComplete(params string[] values) =>
            values != null &&
            values.Length > 0 &&
            Array.TrueForAll(values, value => !string.IsNullOrWhiteSpace(value));

        public static string FormatPhone(string value)
        {
            var digits = Digits(value, 11);
            var result = new StringBuilder(16);

            for (var index = 0; index < digits.Length; index++)
            {
                if (index == 0) result.Append('+');
                else if (index == 1 || index == 4) result.Append(' ');
                else if (index == 7 || index == 9) result.Append('-');

                result.Append(digits[index]);
            }

            return result.ToString();
        }

        public static string FormatExpiry(string value)
        {
            var digits = Digits(value, 4);
            return digits.Length <= 2 ? digits : digits.Insert(2, "/");
        }

        private static string Digits(string value, int limit)
        {
            var result = new StringBuilder(limit);

            for (var index = 0; index < (value?.Length ?? 0) && result.Length < limit; index++)
            {
                if (value[index] >= '0' && value[index] <= '9') result.Append(value[index]);
            }

            return result.ToString();
        }
    }

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
        private GameObject _checkoutPanel;
        private GameObject _successPanel;
        private ARPlacementController _arPlacement;
        private UnityEngine.UI.Button _arButton;
        private readonly List<TMP_InputField> _checkoutFields = new List<TMP_InputField>();
        private UnityEngine.UI.Image _productImage;
        private TextMeshProUGUI _productName;
        private TextMeshProUGUI _productPrice;
        private TextMeshProUGUI _priceLabel;
        private TextMeshProUGUI _checkoutProductName;
        private TextMeshProUGUI _checkoutProductPrice;
        private TextMeshProUGUI _checkoutError;
        private TextMeshProUGUI _successMessage;
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
            BuildCheckoutPanel();
            BuildSuccessPanel();
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

            var checkoutButton = CreateButton(_productPanel.transform, "Checkout Button", "Оформить", ShowCheckout);
            SetRect(checkoutButton.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0), new Vector2(48, 50), new Vector2(-48, 150));
        }

        private void BuildCheckoutPanel()
        {
            _checkoutPanel = CreatePanel("Checkout Panel", transform, Background);
            _checkoutPanel.SetActive(false);

            var back = CreateButton(_checkoutPanel.transform, "Back Button", "‹ Товар", ShowProductPanel);
            SetRect(back.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -116), new Vector2(290, -36));

            var title = CreateText("Title", _checkoutPanel.transform, "Оформление", 48, FontStyles.Bold);
            title.alignment = TextAlignmentOptions.MidlineRight;
            SetRect(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(320, -116), new Vector2(-48, -36));

            _checkoutProductName = CreateText("Product Name", _checkoutPanel.transform, string.Empty, 36, FontStyles.Bold);
            SetRect(_checkoutProductName.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(48, -194), new Vector2(-48, -136));

            _checkoutProductPrice = CreateText("Product Price", _checkoutPanel.transform, string.Empty, 32);
            _checkoutProductPrice.color = Accent;
            SetRect(_checkoutProductPrice.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(48, -244), new Vector2(-48, -194));

            var note = CreateText("Privacy Note", _checkoutPanel.transform, "Демо: данные не отправляются и не сохраняются", 23);
            note.color = new Color32(92, 98, 102, 255);
            SetRect(note.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(48, -292), new Vector2(-48, -250));

            _checkoutFields.Add(CreateInputField(_checkoutPanel.transform, "Name Input", "Имя", "Введите имя", TMP_InputField.ContentType.Name, 80, -326));
            _checkoutFields.Add(CreateInputField(_checkoutPanel.transform, "Phone Input", "Телефон", "+7 000 000-00-00", TMP_InputField.ContentType.Custom, 16, -506, CheckoutValidation.FormatPhone));
            _checkoutFields.Add(CreateInputField(_checkoutPanel.transform, "Address Input", "Адрес", "Введите адрес", TMP_InputField.ContentType.Standard, 160, -686));
            _checkoutFields.Add(CreateInputField(_checkoutPanel.transform, "Card Input", "Номер карты", "0000 0000 0000 0000", TMP_InputField.ContentType.IntegerNumber, 16, -866));
            _checkoutFields.Add(CreateInputField(_checkoutPanel.transform, "Expiry Input", "Срок действия", "ММ/ГГ", TMP_InputField.ContentType.Custom, 5, -1046, CheckoutValidation.FormatExpiry));
            _checkoutFields.Add(CreateInputField(_checkoutPanel.transform, "CVV Input", "CVV", "000", TMP_InputField.ContentType.IntegerNumber, 3, -1226));

            _checkoutError = CreateText("Validation Error", _checkoutPanel.transform, string.Empty, 24);
            _checkoutError.color = new Color32(176, 48, 48, 255);
            _checkoutError.alignment = TextAlignmentOptions.Center;
            SetRect(_checkoutError.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(48, 174), new Vector2(-48, 226));

            var payButton = CreateButton(_checkoutPanel.transform, "Pay Button", "Оплатить", SubmitCheckout);
            SetRect(payButton.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1, 0), new Vector2(48, 50), new Vector2(-48, 150));
        }

        private void BuildSuccessPanel()
        {
            _successPanel = CreatePanel("Success Panel", transform, Background);
            _successPanel.SetActive(false);

            var title = CreateText("Title", _successPanel.transform, "Заказ оформлен", 54, FontStyles.Bold);
            title.alignment = TextAlignmentOptions.Center;
            SetRect(title.rectTransform, new Vector2(0, 0.58f), new Vector2(1, 0.72f), new Vector2(48, 0), new Vector2(-48, 0));

            _successMessage = CreateText("Message", _successPanel.transform, string.Empty, 32);
            _successMessage.alignment = TextAlignmentOptions.Center;
            _successMessage.overflowMode = TextOverflowModes.Overflow;
            SetRect(_successMessage.rectTransform, new Vector2(0, 0.36f), new Vector2(1, 0.58f), new Vector2(64, 0), new Vector2(-64, 0));

            var productButton = CreateButton(_successPanel.transform, "Product Button", "К товару", ShowProductPanel);
            SetRect(productButton.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1, 0), new Vector2(48, 174), new Vector2(-48, 274));

            var catalogButton = CreateButton(_successPanel.transform, "Catalog Button", "В каталог", ShowCatalog);
            SetRect(catalogButton.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1, 0), new Vector2(48, 50), new Vector2(-48, 150));
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
            _checkoutPanel.SetActive(false);
            _successPanel.SetActive(false);
            _arPanel.SetActive(false);
            _productPanel.SetActive(true);
        }

        private void ShowCatalog()
        {
            _arPlacement?.Exit();
            ClearCheckout();
            _productPanel.SetActive(false);
            _checkoutPanel.SetActive(false);
            _successPanel.SetActive(false);
            _arPanel.SetActive(false);
            _catalogPanel.SetActive(true);
        }

        private void ShowAR()
        {
            if (_arPlacement == null || SelectedProduct?.prefab == null)
            {
                return;
            }

            _productPanel.SetActive(false);
            _checkoutPanel.SetActive(false);
            _successPanel.SetActive(false);
            _arPanel.SetActive(true);
            _arPlacement.Enter(SelectedProduct.prefab);
        }

        private void ShowProductPanel()
        {
            _arPlacement?.Exit();
            ClearCheckout();
            _catalogPanel.SetActive(false);
            _arPanel.SetActive(false);
            _checkoutPanel.SetActive(false);
            _successPanel.SetActive(false);
            _productPanel.SetActive(true);
        }

        private void ShowCheckout()
        {
            if (SelectedProduct == null)
            {
                return;
            }

            ClearCheckout();
            _checkoutProductName.text = SelectedProduct.name;
            _checkoutProductPrice.text = FormatPrice(SelectedProduct.price);
            _productPanel.SetActive(false);
            _checkoutPanel.SetActive(true);
        }

        private void SubmitCheckout()
        {
            var values = _checkoutFields.ConvertAll(field => field.text).ToArray();
            if (!CheckoutValidation.IsComplete(values))
            {
                _checkoutError.text = "Заполните все обязательные поля";
                return;
            }

            _successMessage.text = $"{SelectedProduct.name}\n{FormatPrice(SelectedProduct.price)}\n\nСпасибо!";
            ClearCheckout();
            _checkoutPanel.SetActive(false);
            _successPanel.SetActive(true);
        }

        private void ClearCheckout()
        {
            foreach (var field in _checkoutFields)
            {
                field.SetTextWithoutNotify(string.Empty);
            }

            if (_checkoutError != null)
            {
                _checkoutError.text = string.Empty;
            }
        }

        private TMP_InputField CreateInputField(
            Transform parent,
            string name,
            string label,
            string placeholder,
            TMP_InputField.ContentType contentType,
            int characterLimit,
            float top,
            Func<string, string> formatter = null)
        {
            var labelText = CreateText(name + " Label", parent, label, 26);
            SetRect(labelText.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(48, top - 44), new Vector2(-48, top));

            var fieldObject = CreateObject(name, parent);
            SetRect(fieldObject.GetComponent<RectTransform>(), new Vector2(0, 1), Vector2.one, new Vector2(48, top - 142), new Vector2(-48, top - 54));
            var background = fieldObject.AddComponent<UnityEngine.UI.Image>();
            background.color = Surface;

            var viewport = CreateObject("Text Area", fieldObject.transform);
            SetRect(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(22, 8), new Vector2(-22, -8));
            viewport.AddComponent<UnityEngine.UI.RectMask2D>();

            var inputText = CreateText("Text", viewport.transform, string.Empty, 28);
            inputText.overflowMode = TextOverflowModes.Overflow;
            SetRect(inputText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var placeholderText = CreateText("Placeholder", viewport.transform, placeholder, 28);
            placeholderText.color = new Color32(142, 146, 148, 255);
            SetRect(placeholderText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var input = fieldObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.textViewport = viewport.GetComponent<RectTransform>();
            input.textComponent = inputText;
            input.placeholder = placeholderText;
            input.contentType = contentType;
            input.characterLimit = characterLimit;
            input.lineType = TMP_InputField.LineType.SingleLine;

            if (formatter != null)
            {
                input.characterValidation = TMP_InputField.CharacterValidation.Digit;
                input.keyboardType = TouchScreenKeyboardType.NumberPad;
                input.onValueChanged.AddListener(value =>
                {
                    var formatted = formatter(value);
                    if (formatted == value) return;

                    input.SetTextWithoutNotify(formatted);
                    input.caretPosition = formatted.Length;
                });
            }

            return input;
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
