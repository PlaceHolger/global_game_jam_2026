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
    public int badge;
    public int team;
    public int productionBoost = 100;
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

    public const int ProductionMin = 100;
    public const int ProductionMax = 200;
    public const int ProductionStep = 5;

    /// <summary>Last applied matchup rating; 0 when there is no AI opposition.</summary>
    public static float Difficulty { get; private set; }

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
        int homeCount = MapLayout.Count(mapScene);
        for (int i = 0; i < copy.Length; i++)
        {
            copy[i].home = Mathf.Clamp(copy[i].home, 0, homeCount - 1);
            copy[i].color = Mathf.Clamp(copy[i].color, 0, Palette.Length - 1);
            copy[i].ship = Mathf.Max(0, copy[i].ship);
            copy[i].badge = Mathf.Max(0, copy[i].badge);
            copy[i].team = EffectiveTeam(copy[i], i);
            copy[i].productionBoost = Mathf.Clamp(BoostPercent(copy[i]), ProductionMin, ProductionMax);
        }

        _slots = copy;
        MapScene = mapScene;
        Difficulty = EvaluateDifficulty(copy);

        Globals globals = Globals.Instance;
        if (!globals)
            return;

        globals.playerCount = ActiveCount(copy);
        globals.ApplyMatchCosmetics(ColorsOf(copy), ShipsOf(copy), BadgesOf(copy), RotationsOf(copy));
    }

    public static void SetSlotHuman(int slotIndex, bool human, MatchInput input = MatchInput.Keyboard)
    {
        if (_slots == null || slotIndex < 0 || slotIndex >= _slots.Length)
            return;
        _slots[slotIndex].mode = human ? MatchSlotMode.Human : MatchSlotMode.Ai;
        if (human)
            _slots[slotIndex].input = input;
        Globals globals = Globals.Instance;
        if (globals)
        {
            int humans = 0;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].IsHuman)
                    humans++;
            globals.localMultiplayer = humans > 1;
        }
    }

    public static void Reset()
    {
        _slots = null;
        _mapScene = "LargeMap";
        Difficulty = 0f;
        if (Globals.Instance)
            Globals.Instance.ApplyMatchCosmetics(null, null, null, null);
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

    public static int EffectiveTeam(MatchSlot slot, int index) =>
        slot != null && slot.team > 0 ? Mathf.Clamp(slot.team, 1, MaxSlots) : index + 1;

    public static int TeamOf(Globals.ePlayer player)
    {
        int index = (int)player - 1;
        if (index < 0 || index >= MaxSlots)
            return 0;

        MatchSlot[] slots = _slots;
        return slots != null ? EffectiveTeam(slots[index], index) : index + 1;
    }

    public static bool AreAllied(Globals.ePlayer a, Globals.ePlayer b)
    {
        if (a == Globals.ePlayer.None || b == Globals.ePlayer.None)
            return false;
        if (a == b)
            return true;

        int indexA = (int)a - 1;
        int indexB = (int)b - 1;
        if (indexA < 0 || indexB < 0 || indexA >= MaxSlots || indexB >= MaxSlots || _slots == null)
            return false;

        return EffectiveTeam(_slots[indexA], indexA) == EffectiveTeam(_slots[indexB], indexB);
    }

    public static bool AreEnemies(Globals.ePlayer a, Globals.ePlayer b) =>
        a != Globals.ePlayer.None && b != Globals.ePlayer.None && a != b && !AreAllied(a, b);

    public static List<Globals.ePlayer> TeamMembers(int team)
    {
        var result = new List<Globals.ePlayer>();
        MatchSlot[] slots = Current;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].mode != MatchSlotMode.Off && EffectiveTeam(slots[i], i) == team)
                result.Add((Globals.ePlayer)(i + 1));
        }
        return result;
    }

    public static bool TeamHasHuman(int team)
    {
        foreach (Globals.ePlayer player in TeamMembers(team))
            if (IsHuman(player))
                return true;
        return false;
    }

    public static float ProductionBoost(Globals.ePlayer player)
    {
        int index = (int)player - 1;
        if (_slots == null || index < 0 || index >= _slots.Length)
            return 1f;
        return Mathf.Clamp(BoostPercent(_slots[index]), ProductionMin, ProductionMax) / 100f;
    }

    /// <summary>
    /// A rough matchup rating: hostile AI power over human-side power. Team cohesion,
    /// AI strength, starting ships and production boosts all move the number.
    /// </summary>
    public static float EvaluateDifficulty(MatchSlot[] slots)
    {
        if (slots == null)
            return 0f;

        var teamSize = new int[MaxSlots + 1];
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].mode != MatchSlotMode.Off)
                teamSize[EffectiveTeam(slots[i], i)]++;

        float humanPower = 0f;
        float aiPower = 0f;
        for (int i = 0; i < slots.Length; i++)
        {
            MatchSlot slot = slots[i];
            if (slot.mode == MatchSlotMode.Off)
                continue;

            int team = EffectiveTeam(slot, i);
            float power = 1f + Mathf.Max(0, BoostPercent(slot) - 100) / 100f;

            if (slot.IsHuman)
            {
                humanPower += power;
                continue;
            }

            float weight = StrengthWeight(slot.strength);
            if (TeamHasHuman(slots, team))
            {
                humanPower += power * weight * 0.5f;
                continue;
            }

            float cohesion = 1f + 0.25f * (teamSize[team] - 1);
            aiPower += power * weight * cohesion;
        }

        return aiPower <= 0f
            ? 0f
            : Mathf.Clamp(aiPower / Mathf.Max(0.5f, humanPower), 0f, 99f);
    }

    private static bool TeamHasHuman(MatchSlot[] slots, int team)
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].mode == MatchSlotMode.Human && EffectiveTeam(slots[i], i) == team)
                return true;
        return false;
    }

    private static float StrengthWeight(MatchAiStrength strength)
    {
        switch (strength)
        {
            case MatchAiStrength.Easy: return 0.7f;
            case MatchAiStrength.Hard: return 1.4f;
            default: return 1f;
        }
    }

    private static int BoostPercent(MatchSlot slot) =>
        slot == null || slot.productionBoost <= 0 ? ProductionMin : slot.productionBoost;

    public static bool IsValid(MatchSlot[] slots, out string error) =>
        IsValid(slots, MapScene, out error);

    public static bool IsValid(MatchSlot[] slots, string mapScene, out string error)
    {
        int active = 0;
        int homeCount = MapLayout.Count(mapScene);
        bool[] homes = new bool[homeCount];
        bool[] inputs = new bool[Enum.GetValues(typeof(MatchInput)).Length];
        bool[] teams = new bool[MaxSlots + 1];

        for (int i = 0; i < slots.Length; i++)
        {
            MatchSlot slot = slots[i];
            if (slot.mode == MatchSlotMode.Off)
                continue;

            active++;
            teams[EffectiveTeam(slot, i)] = true;
            if (slot.home < 0 || slot.home >= homeCount)
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

        int teamCount = 0;
        for (int i = 1; i < teams.Length; i++)
            if (teams[i])
                teamCount++;
        if (teamCount < 2)
        {
            error = "Choose at least two teams.";
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
                badge = badges > 0 ? i % badges : 0,
                team = i + 1,
                productionBoost = 100,
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
            badges[i] = globals.GetBadgeByIndex(index);
        }
        return badges;
    }

    private static float[] RotationsOf(MatchSlot[] slots)
    {
        var rotations = new float[MaxSlots + 1];
        Globals globals = Globals.Instance;
        if (!globals)
            return rotations;

        for (int i = 1; i <= MaxSlots; i++)
            rotations[i] = globals.GetShipRotationByIndex(slots[i - 1].ship);
        return rotations;
    }
}
