using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Milkfrog.CombatDemo
{
    public sealed class MvpMenuView : MonoBehaviour
    {
        private const int MaxActions = 5;

        private static readonly Color PanelColor = new Color32(17, 23, 31, 248);
        private static readonly Color ButtonColor = new Color32(43, 52, 64, 255);
        private static readonly Color ButtonHighlightColor = new Color32(65, 75, 87, 255);
        private static readonly Color ButtonPressedColor = new Color32(89, 72, 48, 255);
        private static readonly Color ButtonDisabledColor = new Color32(43, 47, 53, 210);
        private static readonly Color TitleColor = new Color32(235, 190, 111, 255);
        private static readonly Color BodyColor = new Color32(222, 225, 230, 255);

        private readonly List<Button> _buttons = new List<Button>(MaxActions);
        private GameObject _root;
        private Text _titleText;
        private Text _subtitleText;
        private EventSystem _eventSystem;
        private GameObject _createdEventSystem;
        private Coroutine _selectionRoutine;

        public bool Visible => _root != null && _root.activeSelf;

        public void Show(string title, string subtitle, params MenuAction[] actions)
        {
            EnsureView();
            EnsureEventSystem();

            _titleText.text = title ?? string.Empty;
            _subtitleText.text = subtitle ?? string.Empty;

            int actionCount = Mathf.Min(actions?.Length ?? 0, MaxActions);
            for (int i = 0; i < _buttons.Count; i++)
            {
                Button button = _buttons[i];
                button.onClick.RemoveAllListeners();

                bool hasAction = i < actionCount;
                button.gameObject.SetActive(hasAction);
                if (!hasAction)
                {
                    continue;
                }

                MenuAction action = actions[i];
                Text label = button.GetComponentInChildren<Text>();
                label.text = action.Label ?? string.Empty;
                button.interactable = action.Enabled;

                Action callback = action.Callback;
                if (callback != null)
                {
                    button.onClick.AddListener(() => callback());
                }
            }

            _root.SetActive(true);
            ClearCurrentSelection();
            ScheduleInitialSelection();
        }

        public void Hide()
        {
            if (_selectionRoutine != null)
            {
                StopCoroutine(_selectionRoutine);
                _selectionRoutine = null;
            }

            ClearCurrentSelection();
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_selectionRoutine != null)
            {
                StopCoroutine(_selectionRoutine);
                _selectionRoutine = null;
            }

            ClearCurrentSelection();
            if (_root != null)
            {
                Destroy(_root);
                _root = null;
            }

            if (_createdEventSystem != null)
            {
                Destroy(_createdEventSystem);
                _createdEventSystem = null;
                _eventSystem = null;
            }
        }

        private void EnsureView()
        {
            if (_root != null)
            {
                return;
            }

            _root = new GameObject(
                "MvpMenuCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform rootRect = _root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject panel = CreateUiObject("Panel", _root.transform, typeof(Image));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            SetCenteredRect(panelRect, new Vector2(840f, 620f), Vector2.zero);
            panel.GetComponent<Image>().color = PanelColor;

            _titleText = CreateText("Title", panel.transform, font, 34, FontStyle.Bold,
                TextAnchor.MiddleCenter, TitleColor, new Vector2(740f, 58f), new Vector2(0f, 248f));
            _subtitleText = CreateText("Subtitle", panel.transform, font, 22, FontStyle.Normal,
                TextAnchor.MiddleCenter, BodyColor, new Vector2(720f, 104f), new Vector2(0f, 170f));

            for (int i = 0; i < MaxActions; i++)
            {
                _buttons.Add(CreateButton(i, panel.transform, font));
            }

            _root.SetActive(false);
        }

        private Button CreateButton(int index, Transform parent, Font font)
        {
            GameObject buttonObject = CreateUiObject("Action " + (index + 1), parent, typeof(Image), typeof(Button));
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            float listCenterY = -80f;
            float rowHeight = 62f;
            float rowSpacing = 12f;
            float totalHeight = MaxActions * rowHeight + (MaxActions - 1) * rowSpacing;
            float firstCenterY = listCenterY + totalHeight * 0.5f - rowHeight * 0.5f;
            SetCenteredRect(buttonRect, new Vector2(720f, rowHeight),
                new Vector2(0f, firstCenterY - index * (rowHeight + rowSpacing)));

            Image background = buttonObject.GetComponent<Image>();
            background.color = ButtonColor;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = ButtonHighlightColor;
            colors.selectedColor = ButtonHighlightColor;
            colors.pressedColor = ButtonPressedColor;
            colors.disabledColor = ButtonDisabledColor;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;

            // A single centered column makes Automatic navigation travel vertically through enabled buttons.
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            navigation.wrapAround = false;
            button.navigation = navigation;

            CreateText("Label", buttonObject.transform, font, 24, FontStyle.Normal,
                TextAnchor.MiddleCenter, BodyColor, new Vector2(680f, 54f), Vector2.zero);
            return button;
        }

        private Text CreateText(string objectName, Transform parent, Font font, int fontSize,
            FontStyle style, TextAnchor alignment, Color color, Vector2 size, Vector2 position)
        {
            GameObject textObject = CreateUiObject(objectName, parent, typeof(Text));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            SetCenteredRect(rect, size, position);

            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent, params Type[] components)
        {
            GameObject uiObject = new GameObject(objectName, components);
            uiObject.transform.SetParent(parent, false);
            return uiObject;
        }

        private static void SetCenteredRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private void EnsureEventSystem()
        {
            if (_eventSystem != null)
            {
                return;
            }

            _eventSystem = EventSystem.current;
            if (_eventSystem == null)
            {
                _eventSystem = FindFirstObjectByType<EventSystem>();
            }

            if (_eventSystem != null)
            {
                return;
            }

            _createdEventSystem = new GameObject("MvpMenuEventSystem", typeof(EventSystem));
            _eventSystem = _createdEventSystem.GetComponent<EventSystem>();
            InputSystemUIInputModule inputModule = _createdEventSystem.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }

        private void ScheduleInitialSelection()
        {
            if (_selectionRoutine != null)
            {
                StopCoroutine(_selectionRoutine);
            }

            if (isActiveAndEnabled)
            {
                _selectionRoutine = StartCoroutine(SelectFirstButtonNextFrame());
            }
            else
            {
                SelectFirstButton();
            }
        }

        private IEnumerator SelectFirstButtonNextFrame()
        {
            yield return null;
            _selectionRoutine = null;
            if (Visible)
            {
                SelectFirstButton();
            }
        }

        private void SelectFirstButton()
        {
            EventSystem eventSystem = EventSystem.current;
            Button firstButton = GetFirstAvailableButton();
            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(firstButton != null ? firstButton.gameObject : null);
            }
        }

        private void ClearCurrentSelection()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
            {
                return;
            }

            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] != null && eventSystem.currentSelectedGameObject == _buttons[i].gameObject)
                {
                    eventSystem.SetSelectedGameObject(null);
                    return;
                }
            }
        }

        private Button GetFirstAvailableButton()
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                Button button = _buttons[i];
                if (button != null && button.isActiveAndEnabled && button.interactable)
                {
                    return button;
                }
            }

            return null;
        }
    }

    public readonly struct MenuAction
    {
        public readonly string Label;
        public readonly Action Callback;
        public readonly bool Enabled;

        public MenuAction(string label, Action callback, bool enabled = true)
        {
            Label = label;
            Callback = callback;
            Enabled = enabled;
        }
    }
}
