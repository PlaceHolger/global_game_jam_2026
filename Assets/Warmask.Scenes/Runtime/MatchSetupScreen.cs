using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Warmask.Scenes
{
    /// <summary>
    /// Full-screen match setup built at runtime, so the menu scene stays a single
    /// Start button and the map/config choices live here.
    /// </summary>
    public class MatchSetupScreen : MonoBehaviour
    {
        private enum Row
        {
            Mode,
            Home,
            Ship,
            Color,
            Badge,
            Strength,
            Type,
            Input,
            Count
        }

        private sealed class ColumnUi
        {
            public TMP_Text Header;
            public readonly Button[] Buttons = new Button[(int)Row.Count];
            public readonly TMP_Text[] Labels = new TMP_Text[(int)Row.Count];
        }

        private static MatchSetupScreen _instance;

        private MatchSlot[] _draft;
        private string _map = "LargeMap";
        private TMP_Text _mapLabel;
        private TMP_Text _hint;
        private readonly ColumnUi[] _columns = new ColumnUi[MatchConfig.MaxSlots];

        private static readonly Color PanelColor = new Color(0.05f, 0.07f, 0.12f, 0.94f);
        private static readonly Color RowColor = new Color(0.13f, 0.17f, 0.26f, 1f);
        private static readonly Color AccentColor = new Color(0.22f, 0.62f, 0.95f, 1f);

        public static void Open()
        {
            if (!_instance)
            {
                var go = new GameObject("Match Setup", typeof(RectTransform));
                _instance = go.AddComponent<MatchSetupScreen>();
            }

            _instance.Show();
        }

        private void Awake()
        {
            if (_instance && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            Build();
        }

        private void Show()
        {
            _draft = MatchConfig.CloneCurrent();
            _map = MatchConfig.MapScene;
            Normalize(_draft);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Refresh();
        }

        private void Close() => gameObject.SetActive(false);

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Close();
        }

        private void Build()
        {
            gameObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var canvas = gameObject.GetComponent<Canvas>();
            canvas.sortingOrder = 100;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            Image backdrop = NewImage(transform, "Backdrop", Color.white);
            Stretch(backdrop.rectTransform);
            Image menuBackdrop = GameObject.Find("Main Menu/Backdrop")?.GetComponent<Image>();
            if (menuBackdrop && menuBackdrop.sprite)
            {
                backdrop.sprite = menuBackdrop.sprite;
                backdrop.color = new Color(1f, 1f, 1f, 0.5f);
            }
            else
            {
                backdrop.color = new Color(0.015f, 0.02f, 0.04f, 1f);
            }

            Image tint = NewImage(transform, "Tint", new Color(0.01f, 0.02f, 0.04f, 0.78f));
            Stretch(tint.rectTransform);

            BuildHeader();
            BuildColumns();
            BuildFooter();
        }

        private void BuildHeader()
        {
            var header = NewRect(transform, "Header");
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(-80f, 100f);
            header.anchoredPosition = new Vector2(0f, -18f);

            TMP_Text title = NewText(header, "MATCH SETUP", 42f, TextAlignmentOptions.Left);
            Stretch(title.rectTransform, 0f, 420f, 12f, 12f);
            title.fontStyle = FontStyles.Bold;

            TMP_Text subtitle = NewText(header, "Four slots. One last player standing.", 16f, TextAlignmentOptions.Left);
            Stretch(subtitle.rectTransform, 4f, 420f, 0f, 58f);
            subtitle.color = new Color(0.62f, 0.71f, 0.85f, 1f);

            Button mapButton = NewButton(header, "", AccentColor, ToggleMap);
            var mapRect = (RectTransform)mapButton.transform;
            mapRect.anchorMin = mapRect.anchorMax = new Vector2(1f, 0.5f);
            mapRect.pivot = new Vector2(1f, 0.5f);
            mapRect.sizeDelta = new Vector2(300f, 58f);
            mapRect.anchoredPosition = Vector2.zero;
            _mapLabel = mapButton.GetComponentInChildren<TMP_Text>();
        }

        private void BuildColumns()
        {
            var area = NewRect(transform, "Columns");
            Stretch(area, 40f, 40f, 118f, 118f);

            for (int i = 0; i < MatchConfig.MaxSlots; i++)
            {
                var column = NewRect(area, "P" + (i + 1));
                float min = i / (float)MatchConfig.MaxSlots;
                float max = (i + 1) / (float)MatchConfig.MaxSlots;
                column.anchorMin = new Vector2(min, 0f);
                column.anchorMax = new Vector2(max, 1f);
                column.offsetMin = new Vector2(6f, 0f);
                column.offsetMax = new Vector2(-6f, 0f);

                Image panel = column.gameObject.AddComponent<Image>();
                panel.color = PanelColor;

                var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(14, 14, 14, 14);
                layout.spacing = 8f;
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;

                var ui = new ColumnUi();
                _columns[i] = ui;

                ui.Header = NewText(column, "P" + (i + 1), 25f, TextAlignmentOptions.Center);
                ui.Header.fontStyle = FontStyles.Bold;
                var headerLayout = ui.Header.gameObject.AddComponent<LayoutElement>();
                headerLayout.preferredHeight = 64f;

                for (int r = 0; r < (int)Row.Count; r++)
                    CreateCycleButton(column, i, (Row)r);

                column.SetAsFirstSibling();
                column.SetSiblingIndex(i);
            }
        }

        private void BuildFooter()
        {
            var footer = NewRect(transform, "Footer");
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.sizeDelta = new Vector2(-80f, 92f);
            footer.anchoredPosition = new Vector2(0f, 18f);

            Button back = NewButton(footer, "BACK", RowColor, Close);
            var backRect = (RectTransform)back.transform;
            backRect.anchorMin = backRect.anchorMax = new Vector2(0f, 0.5f);
            backRect.pivot = new Vector2(0f, 0.5f);
            backRect.sizeDelta = new Vector2(220f, 56f);
            backRect.anchoredPosition = Vector2.zero;

            _hint = NewText(footer, "", 17f, TextAlignmentOptions.Center);
            Stretch(_hint.rectTransform, 250f, 360f, 8f, 8f);
            _hint.color = new Color(0.62f, 0.71f, 0.85f, 1f);

            Button start = NewButton(footer, "START THE BATTLE", new Color(0.16f, 0.55f, 0.34f, 1f), StartMatch);
            var startRect = (RectTransform)start.transform;
            startRect.anchorMin = startRect.anchorMax = new Vector2(1f, 0.5f);
            startRect.pivot = new Vector2(1f, 0.5f);
            startRect.sizeDelta = new Vector2(340f, 62f);
            startRect.anchoredPosition = Vector2.zero;
        }

        private void CreateCycleButton(RectTransform parent, int slot, Row row)
        {
            Button button = NewButton(parent, "", RowColor, () => Cycle(slot, row));
            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 52f;
            layout.flexibleWidth = 1f;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            label.fontSize = 17f;
            label.alignment = TextAlignmentOptions.Left;
            Stretch(label.rectTransform, 12f, 8f, 4f, 4f);

            _columns[slot].Buttons[(int)row] = button;
            _columns[slot].Labels[(int)row] = label;
        }

        private void Cycle(int slot, Row row)
        {
            MatchSlot selected = _draft[slot];
            switch (row)
            {
                case Row.Mode:
                    selected.mode = (MatchSlotMode)(((int)selected.mode + 1) % 3);
                    if (selected.mode != MatchSlotMode.Off)
                    {
                        EnsureHome(slot);
                        if (selected.mode == MatchSlotMode.Human)
                            EnsureInput(slot);
                    }
                    break;
                case Row.Home:
                    selected.home = NextFreeHome(slot);
                    break;
                case Row.Ship:
                    selected.ship = (selected.ship + 1) % Mathf.Max(1, Globals.Instance.ShipTypeCount);
                    break;
                case Row.Color:
                    selected.color = (selected.color + 1) % MatchConfig.Palette.Length;
                    break;
                case Row.Badge:
                    int badges = Globals.Instance.BadgeCount;
                    selected.badge = badges <= 0 || selected.badge + 1 >= badges ? -1 : selected.badge + 1;
                    break;
                case Row.Strength:
                    selected.strength = (MatchAiStrength)(((int)selected.strength + 1) % 3);
                    break;
                case Row.Type:
                    selected.aiType = (MatchAiType)(((int)selected.aiType + 1) % 3);
                    break;
                case Row.Input:
                    selected.input = NextInput(slot);
                    break;
            }

            Refresh();
        }

        private void ToggleMap()
        {
            _map = _map == "LargeMap" ? "SampleScene" : "LargeMap";
            Refresh();
        }

        private void StartMatch()
        {
            if (!MatchConfig.IsValid(_draft, out string error))
            {
                ShowError(error);
                return;
            }

            MatchConfig.Apply(_draft, _map);
            GameSession.MapScene = _map;

            if (!SceneLoader.Instance)
            {
                ShowError("No SceneLoader in the open scene.");
                return;
            }

            Close();
            _ = SceneLoader.Instance.LoadGameSceneAsync();
        }

        private void Refresh()
        {
            _mapLabel.text = "MAP   " + MapName(_map);
            _hint.text = "Click a row to change it. Start positions and human inputs must be unique.";
            _hint.color = new Color(0.62f, 0.71f, 0.85f, 1f);

            for (int i = 0; i < MatchConfig.MaxSlots; i++)
            {
                MatchSlot slot = _draft[i];
                ColumnUi ui = _columns[i];
                Color color = MatchConfig.Palette[Mathf.Clamp(slot.color, 0, MatchConfig.Palette.Length - 1)];

                ui.Header.text = $"P{i + 1}   {ModeName(slot.mode)}";
                ui.Header.color = slot.mode == MatchSlotMode.Off ? new Color(0.5f, 0.56f, 0.65f) : color;

                SetRow(i, Row.Mode, "MODE", ModeName(slot.mode));
                SetRow(i, Row.Home, "POSITION", slot.mode == MatchSlotMode.Off ? "—" : HomeName(slot.home));
                SetRow(i, Row.Ship, "SHIP", ShipName(slot.ship));
                SetRow(i, Row.Color, "COLOR", MatchConfig.ColorNames[slot.color]);
                SetRow(i, Row.Badge, "BADGE", BadgeName(slot.badge));
                SetRow(i, Row.Strength, "AI STRENGTH", StrengthName(slot.strength));
                SetRow(i, Row.Type, "AI TYPE", AiTypeName(slot.aiType));
                SetRow(i, Row.Input, "INPUT", MatchConfig.InputName(slot.input));

                ui.Buttons[(int)Row.Strength].interactable = slot.mode == MatchSlotMode.Ai;
                ui.Buttons[(int)Row.Type].interactable = slot.mode == MatchSlotMode.Ai;
                ui.Buttons[(int)Row.Input].interactable = slot.mode == MatchSlotMode.Human;
            }
        }

        private void SetRow(int slot, Row row, string name, string value)
        {
            _columns[slot].Labels[(int)row].text =
                $"<size=65%><color=#93A6C2>{name}</color></size>\n<b>{value}</b>";
        }

        private void ShowError(string message)
        {
            _hint.text = message;
            _hint.color = new Color(1f, 0.62f, 0.35f, 1f);
        }

        private void Normalize(MatchSlot[] slots)
        {
            var homes = new bool[MatchConfig.MaxSlots];
            var inputs = new bool[Enum.GetValues(typeof(MatchInput)).Length];

            for (int i = 0; i < slots.Length; i++)
            {
                MatchSlot slot = slots[i];
                if (slot.mode == MatchSlotMode.Off)
                    continue;

                slot.home = Mathf.Clamp(slot.home, 0, MatchConfig.MaxSlots - 1);
                slot.color = Mathf.Clamp(slot.color, 0, MatchConfig.Palette.Length - 1);
                slot.ship = Mathf.Max(0, slot.ship);
                slot.badge = Mathf.Clamp(slot.badge, -1, Mathf.Max(-1, Globals.Instance.BadgeCount - 1));

                if (homes[slot.home])
                    slot.home = FirstFreeHome(homes);
                homes[slot.home] = true;

                if (!slot.IsHuman)
                    continue;

                if (i > 0 && slot.input == MatchInput.MouseKeyboard)
                    slot.input = MatchInput.Keyboard;
                if (inputs[(int)slot.input])
                    slot.input = FirstFreeInput(i, inputs);
                inputs[(int)slot.input] = true;
            }
        }

        private void EnsureHome(int slot)
        {
            int current = _draft[slot].home;
            for (int i = 0; i < MatchConfig.MaxSlots; i++)
            {
                if (i == slot || _draft[i].mode == MatchSlotMode.Off || _draft[i].home != current)
                    continue;
                _draft[slot].home = NextFreeHome(slot);
                return;
            }
        }

        private void EnsureInput(int slot)
        {
            if (!InputUsedByOther(slot, _draft[slot].input))
                return;
            _draft[slot].input = NextInput(slot);
        }

        private int NextFreeHome(int slot)
        {
            for (int offset = 1; offset <= MatchConfig.MaxSlots; offset++)
            {
                int candidate = (_draft[slot].home + offset) % MatchConfig.MaxSlots;
                bool used = false;
                for (int i = 0; i < MatchConfig.MaxSlots; i++)
                {
                    if (i != slot && _draft[i].mode != MatchSlotMode.Off && _draft[i].home == candidate)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                    return candidate;
            }

            return _draft[slot].home;
        }

        private MatchInput NextInput(int slot)
        {
            MatchInput current = _draft[slot].input;
            MatchInput[] allowed = AllowedInputs(slot);
            int start = Array.IndexOf(allowed, current);
            for (int offset = 1; offset <= allowed.Length; offset++)
            {
                MatchInput candidate = allowed[(start + offset) % allowed.Length];
                if (!InputUsedByOther(slot, candidate))
                    return candidate;
            }

            return current;
        }

        private bool InputUsedByOther(int slot, MatchInput input)
        {
            if (slot > 0 && input == MatchInput.MouseKeyboard)
                return true;

            for (int i = 0; i < MatchConfig.MaxSlots; i++)
            {
                if (i != slot && _draft[i].IsHuman && _draft[i].input == input)
                    return true;
            }
            return false;
        }

        private static MatchInput[] AllowedInputs(int slot)
        {
            if (slot == 0)
            {
                return new[]
                {
                    MatchInput.MouseKeyboard, MatchInput.Keyboard,
                    MatchInput.Gamepad1, MatchInput.Gamepad2, MatchInput.Gamepad3, MatchInput.Gamepad4
                };
            }

            return new[]
            {
                MatchInput.Keyboard,
                MatchInput.Gamepad1, MatchInput.Gamepad2, MatchInput.Gamepad3, MatchInput.Gamepad4
            };
        }

        private static int FirstFreeHome(bool[] used)
        {
            for (int i = 0; i < used.Length; i++)
                if (!used[i])
                    return i;
            return 0;
        }

        private MatchInput FirstFreeInput(int slot, bool[] used)
        {
            foreach (MatchInput candidate in AllowedInputs(slot))
            {
                if (!used[(int)candidate])
                    return candidate;
            }
            return MatchInput.Keyboard;
        }

        private string HomeName(int index)
        {
            if (_map == "SampleScene")
            {
                string[] names = { "McPlanetface", "Hollywood", "McNopeFace", "Bad WiFi 8" };
                return names[Mathf.Clamp(index, 0, names.Length - 1)];
            }

            string[] large = { "NW", "SE", "NE", "SW" };
            return large[Mathf.Clamp(index, 0, large.Length - 1)];
        }

        private string ShipName(int index)
        {
            Sprite sprite = Globals.Instance.GetShipSpriteByIndex(index);
            return sprite ? sprite.name.Replace("Ship-", "") : "Default";
        }

        private string BadgeName(int index)
        {
            if (index < 0)
                return "None";
            Sprite badge = Globals.Instance.GetBadgeByIndex(index);
            return badge ? badge.name : "Badge " + (index + 1);
        }

        private static string MapName(string map) =>
            map == "SampleScene" ? "SAMPLE SCENE" : "LARGE MAP";

        private static string ModeName(MatchSlotMode mode)
        {
            switch (mode)
            {
                case MatchSlotMode.Human: return "HUMAN";
                case MatchSlotMode.Ai: return "AI";
                default: return "OFF";
            }
        }

        private static string StrengthName(MatchAiStrength strength)
        {
            switch (strength)
            {
                case MatchAiStrength.Easy: return "EASY";
                case MatchAiStrength.Hard: return "HARD";
                default: return "NORMAL";
            }
        }

        private static string AiTypeName(MatchAiType type)
        {
            switch (type)
            {
                case MatchAiType.Aggressive: return "AGGRESSIVE";
                case MatchAiType.Defensive: return "DEFENSIVE";
                default: return "BALANCED";
            }
        }

        private static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image NewImage(Transform parent, string name, Color color)
        {
            var rect = NewRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text NewText(Transform parent, string text, float size, TextAlignmentOptions alignment)
        {
            var rect = NewRect(parent, "Text");
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        private static Button NewButton(Transform parent, string text, Color color, UnityAction onClick)
        {
            var rect = NewRect(parent, "Button");
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            TMP_Text label = NewText(rect, text, 19f, TextAlignmentOptions.Center);
            Stretch(label.rectTransform, 8f, 8f, 4f, 4f);
            label.fontStyle = FontStyles.Bold;

            return button;
        }

        private static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float bottom = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
