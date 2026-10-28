using System;
using System.Collections.Generic;
using UnityEngine;

public enum MatchSlotMode
{
    Off,
    Human,
    Ai
}

public enum MatchAiStrength
{
    Easy,
    Normal,
    Hard
}

public enum MatchAiType
{
    Balanced,
    Aggressive,
    Defensive
}

public enum MatchInput
{
    MouseKeyboard,
    Keyboard,
    Gamepad1,
    Gamepad2,
    Gamepad3,
    Gamepad4
}

[Serializable]
public class MatchSlot
{
    public MatchSlotMode mode;
    public int home;
    public int color;
    public int ship;
    public int badge = -1;
    public MatchAiStrength strength = MatchAiStrength.Normal;
    public MatchAiType aiType = MatchAiType.Balanced;
    public MatchInput input;

    public bool IsHuman => mode == MatchSlotMode.Human;
    public bool IsAi => mode == MatchSlotMode.Ai;

    public MatchSlot Clone() => (MatchSlot)MemberwiseClone();
}

/// <summary>
/// The setup-screen choices for the next match. Static like GameSession so it
/// survives the menu-to-map scene load. Slots are ordered P1..P4.
/// </summary>
public static class MatchConfig
{
    public const int MaxSlots = 4;

    public static readonly string[] ColorNames =
    {
        "White", "Yellow", "Cyan", "Orange", "Pink", "Green", "Purple", "Red"
    };

    public static readonly Color[] Palette =
    {
        new Color(1f, 1f, 1f),
        new Color(1f, 0.937f, 0.4f),
        new Color(0.278f, 0.888f, 1f),
        new Color(1f, 0.52f, 0.12f),
        new Color(1f, 0.45f, 0.72f),
        new Color(0.35f, 1f, 0.45f),
        new Color(0.7f, 0.4f, 1f),
        new Color(1f, 0.28f, 0.28f)
    };

    private static MatchSlot[] _slots;
    private static string _mapScene = "LargeMap";

    public static bool IsConfigured => _slots != null;

    public static string MapScene
    {
        get => _mapScene;
        set => _mapScene = string.IsNullOrEmpty(value) ? "LargeMap" : value;
    }

    /// <summary>Slots currently used by the match, configured or legacy defaults.</summary>
    public static MatchSlot[] Current => _slots ?? DefaultFor(Globals.Instance ? Globals.Instance.playerCount : 2);

    /// <summary>A copy the setup screen can mutate without changing the live match.</summary>
    public static MatchSlot[] CloneCurrent() => Clone(Current);

    public static void Apply(MatchSlot[] slots, string mapScene)
    {
        MatchSlot[] copy = Clone(slots);
        for (int i = 0; i < copy.Length; i++)
        {
            copy[i].home = Mathf.Clamp(copy[i].home, 0, MaxSlots - 1);
            copy[i].color = Mathf.Clamp(copy[i].color, 0, Palette.Length - 1);
            copy[i].ship = Mathf.Max(0, copy[i].ship);
            copy[i].badge = copy[i].badge < 0 ? -1 : copy[i].badge;
        }

        _slots = copy;
        MapScene = mapScene;

        Globals globals = Globals.Instance;
        if (!globals)
            return;

        globals.playerCount = ActiveCount(copy);
        globals.ApplyMatchCosmetics(ColorsOf(copy), ShipsOf(copy), BadgesOf(copy));
    }

    public static void Reset()
    {
        _slots = null;
        _mapScene = "LargeMap";
        if (Globals.Instance)
            Globals.Instance.ApplyMatchCosmetics(null, null, null);
    }

    public static int ActiveCount(MatchSlot[] slots)
    {
        int count = 0;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].mode != MatchSlotMode.Off)
                count++;
        return count;
    }

    public static bool IsHuman(Globals.ePlayer player)
    {
        int index = (int)player - 1;
        if (index < 0 || index >= MaxSlots)
            return false;
        return Current[index].IsHuman;
    }

    public static List<Globals.ePlayer> ActivePlayers()
    {
        var result = new List<Globals.ePlayer>();
        MatchSlot[] slots = Current;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].mode != MatchSlotMode.Off)
                result.Add((Globals.ePlayer)(i + 1));
        return result;
    }

    public static bool IsValid(MatchSlot[] slots, out string error)
    {
        int active = 0;
        bool[] homes = new bool[MaxSlots];
        bool[] inputs = new bool[Enum.GetValues(typeof(MatchInput)).Length];

        for (int i = 0; i < slots.Length; i++)
        {
            MatchSlot slot = slots[i];
            if (slot.mode == MatchSlotMode.Off)
                continue;

            active++;
            if (slot.home < 0 || slot.home >= MaxSlots)
            {
                error = "Start position is missing.";
                return false;
            }

            if (homes[slot.home])
            {
                error = "Two players share a start position.";
                return false;
            }
            homes[slot.home] = true;

            if (!slot.IsHuman)
                continue;

            if (i > 0 && slot.input == MatchInput.MouseKeyboard)
            {
                error = "Only Player 1 can use the mouse.";
                return false;
            }

            int inputIndex = (int)slot.input;
            if (inputs[inputIndex])
            {
                error = "Two humans share an input device.";
                return false;
            }
            inputs[inputIndex] = true;
        }

        if (active < 2)
        {
            error = "Choose at least two players.";
            return false;
        }

        error = null;
        return true;
    }

    public static string InputName(MatchInput input)
    {
        switch (input)
        {
            case MatchInput.MouseKeyboard: return "Mouse + Keys";
            case MatchInput.Keyboard: return "Keyboard WASD";
            case MatchInput.Gamepad1: return "Gamepad 1";
            case MatchInput.Gamepad2: return "Gamepad 2";
            case MatchInput.Gamepad3: return "Gamepad 3";
            default: return "Gamepad 4";
        }
    }

    private static MatchSlot[] DefaultFor(int playerCount)
    {
        var slots = new MatchSlot[MaxSlots];
        int badges = Globals.Instance ? Globals.Instance.BadgeCount : 0;
        int ships = Globals.Instance ? Globals.Instance.ShipTypeCount : 1;

        for (int i = 0; i < MaxSlots; i++)
        {
            slots[i] = new MatchSlot
            {
                mode = MatchSlotMode.Off,
                home = i,
                color = i,
                ship = ships > 0 ? i % ships : 0,
                badge = badges > 0 ? i % badges : -1,
                input = i == 0 ? MatchInput.MouseKeyboard : (MatchInput)((int)MatchInput.Gamepad1 + Mathf.Max(0, i - 1))
            };
        }

        slots[0].mode = MatchSlotMode.Human;
        slots[0].input = MatchInput.MouseKeyboard;

        if (playerCount >= 2)
        {
            slots[1].mode = MatchSlotMode.Ai;
            slots[1].input = MatchInput.Gamepad1;
        }
        if (playerCount >= 3)
        {
            slots[2].mode = MatchSlotMode.Ai;
            slots[2].input = MatchInput.Gamepad2;
        }
        if (playerCount >= 4)
        {
            slots[3].mode = MatchSlotMode.Ai;
            slots[3].input = MatchInput.Gamepad3;
        }

        return slots;
    }

    private static MatchSlot[] Clone(MatchSlot[] source)
    {
        var copy = new MatchSlot[MaxSlots];
        for (int i = 0; i < MaxSlots; i++)
        {
            MatchSlot slot = source != null && i < source.Length ? source[i] : null;
            copy[i] = slot != null ? slot.Clone() : new MatchSlot { mode = MatchSlotMode.Off, home = i };
        }
        return copy;
    }

    private static Color[] ColorsOf(MatchSlot[] slots)
    {
        var colors = new Color[MaxSlots + 1];
        for (int i = 1; i <= MaxSlots; i++)
            colors[i] = Palette[Mathf.Clamp(slots[i - 1].color, 0, Palette.Length - 1)];
        return colors;
    }

    private static Sprite[] ShipsOf(MatchSlot[] slots)
    {
        var sprites = new Sprite[MaxSlots + 1];
        Globals globals = Globals.Instance;
        if (!globals)
            return sprites;

        for (int i = 1; i <= MaxSlots; i++)
            sprites[i] = globals.GetShipSpriteByIndex(slots[i - 1].ship);
        return sprites;
    }

    private static Sprite[] BadgesOf(MatchSlot[] slots)
    {
        var badges = new Sprite[MaxSlots + 1];
        Globals globals = Globals.Instance;
        if (!globals)
            return badges;

        for (int i = 1; i <= MaxSlots; i++)
        {
            int index = slots[i - 1].badge;
            badges[i] = index >= 0 ? globals.GetBadgeByIndex(index) : null;
        }
        return badges;
    }
}
