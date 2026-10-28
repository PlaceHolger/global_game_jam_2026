using UnityEngine;

/// <summary>
/// Planet names and positions used by the setup screen's map picker. The order
/// is also the home index order that FourPlayerSetup resolves by name, so keep
/// it in sync with the map scenes.
/// </summary>
public sealed class MapPlanet
{
    public readonly string name;
    public readonly Vector2 position;

    public MapPlanet(string name, float x, float y)
    {
        this.name = name;
        position = new Vector2(x, y);
    }
}

public static class MapLayout
{
    public const string LargeMapScene = "LargeMap";
    public const string SampleScene = "SampleScene";

    private static readonly MapPlanet[] LargeMap =
    {
        new MapPlanet("Tatooine Adjacent", -16f, 7f),
        new MapPlanet("Wolf 359", 16f, -7f),
        new MapPlanet("LV-426", 16f, 7f),
        new MapPlanet("Arrakis Springs", -16f, -7f),
        new MapPlanet("Naboo Neighbor", 11f, 3.5f),
        new MapPlanet("Kessel Runway", -11f, 3.5f),
        new MapPlanet("Nostromo Salvage", 11f, -3.5f),
        new MapPlanet("Risa Holiday", -11f, -3.5f),
        new MapPlanet("Darmok and Jalad", 7f, 8f),
        new MapPlanet("Sulaco Scrapyard", -7f, 8f),
        new MapPlanet("Xenomorph Park", 7f, -8f),
        new MapPlanet("Hoth Adjacent", -7f, -8f),
        new MapPlanet("Omicron Persei 8", 19f, 2f),
        new MapPlanet("Deep Space Nine-ish", -19f, 2f),
        new MapPlanet("Jupiter 2", 19f, -2f),
        new MapPlanet("Mos Eisley Municipal", -19f, -2f),
        new MapPlanet("Galactic Centerfold", 0f, 0f)
    };

    private static readonly MapPlanet[] SampleScenePlanets =
    {
        new MapPlanet("Planet McPlanetface", -10.97f, 5.42f),
        new MapPlanet("Hollywood", 5.8f, 4.95f),
        new MapPlanet("McNopeFace", 10.33f, -5.26f),
        new MapPlanet("Bad WiFi 8", -5.79f, -4.01f),
        new MapPlanet("Planet Jackson", -5.96f, 4.42f),
        new MapPlanet("404 Planet Not Found", -10.68f, 1.09f),
        new MapPlanet("Planet Dammit Janet", -7.8f, -6.2f),
        new MapPlanet("Definitely Not a Trap", -3.8f, -6.4f),
        new MapPlanet("He-Who-Must-Not-Be-Named", 5.86f, -2.74f),
        new MapPlanet("😉", 10.07f, -0.4f),
        new MapPlanet("Center of the Universe", 0.05f, 0.1f)
    };

    public static MapPlanet[] For(string sceneName) =>
        sceneName == SampleScene ? SampleScenePlanets : LargeMap;

    public static int Count(string sceneName) => For(sceneName).Length;
}
