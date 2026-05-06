using UnityEngine;

public static class WeatherSystemsSaveLoad
{
    public static WeatherSystemsSaveData SaveState()
    {
        WeatherSystemsSaveData data = new WeatherSystemsSaveData();

        WeatherGridManager weatherGrid = Object.FindObjectOfType<WeatherGridManager>(true);
        CloudSimulationSystem clouds = Object.FindObjectOfType<CloudSimulationSystem>(true);
        RainSimulationSystem rain = Object.FindObjectOfType<RainSimulationSystem>(true);
        StormSimulationSystem storms = Object.FindObjectOfType<StormSimulationSystem>(true);

        TornadoSimulationSystem tornadoes = Object.FindObjectOfType<TornadoSimulationSystem>(true);
        LightningSimulationSystem lightning = Object.FindObjectOfType<LightningSimulationSystem>(true);

        data.weatherGridData = weatherGrid != null ? weatherGrid.SaveState() : null;
        data.cloudData = clouds != null ? clouds.SaveState() : null;
        data.rainData = rain != null ? rain.SaveState() : null;
        data.stormData = storms != null ? storms.SaveState() : null;

        data.tornadoData = tornadoes != null ? tornadoes.SaveState() : null;
        data.lightningData = lightning != null ? lightning.SaveState() : null;

        return data;
    }

    public static void LoadState(WeatherSystemsSaveData data)
    {
        if (data == null)
            return;

        WeatherGridManager weatherGrid = Object.FindObjectOfType<WeatherGridManager>(true);
        CloudSimulationSystem clouds = Object.FindObjectOfType<CloudSimulationSystem>(true);
        RainSimulationSystem rain = Object.FindObjectOfType<RainSimulationSystem>(true);
        StormSimulationSystem storms = Object.FindObjectOfType<StormSimulationSystem>(true);

        TornadoSimulationSystem tornadoes = Object.FindObjectOfType<TornadoSimulationSystem>(true);
        LightningSimulationSystem lightning = Object.FindObjectOfType<LightningSimulationSystem>(true);

        // Load order matters:
        // 1. Weather grid first.
        // 2. Clouds/rain/storms.
        // 3. Tornado/lightning after storms/clouds exist.

        if (weatherGrid != null)
            weatherGrid.LoadState(data.weatherGridData);

        if (clouds != null)
            clouds.LoadState(data.cloudData);

        if (rain != null)
            rain.LoadState(data.rainData);

        if (storms != null)
            storms.LoadState(data.stormData);

        if (tornadoes != null)
            tornadoes.LoadState(data.tornadoData);

        if (lightning != null)
            lightning.LoadState(data.lightningData);
    }
}