using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;
using System.Text;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public class ControlMapConfiguration
{
    [YamlMember(Alias = "maps", ApplyNamingConventions = false)]
    public List<Maps> mapList { get; set; }

    public class ControlMap
    {
        [YamlMember(Alias = "control", ApplyNamingConventions = false)]
        public string RealControl = ""; //quest-x-button
        public string Path = "";

        public ControlMap()
        {
        }
        public ControlMap(string realControl, string path = "")
        {
            RealControl = realControl;
            Path = path;
        }

    }

    public ControlMapConfiguration.Maps GetMap(string mameControl)
    {
        return mapList.FirstOrDefault(map => map.mameControl == mameControl);
    }
    public bool Exists(string mameControl)
    {
        return mapList.Any(map => map.mameControl == mameControl);
    }

    public class Maps
    {
        [YamlMember(Alias = "libretro-id", ApplyNamingConventions = false)]
        public string mameControl;
        public string behavior = "button"; // or axis 
        [YamlMember(Alias = "maps-to", ApplyNamingConventions = false)]
        public List<ControlMap> controlMaps { get; set; }
        public Maps()
        {

        }
        public Maps(string mameControl, string behavior = "button")
        {
            this.mameControl = mameControl;
            this.behavior = behavior;
            this.controlMaps = new();
        }
        public ControlMap GetAction(string realControl)
        {
            return controlMaps.SingleOrDefault(ctrlMap => ctrlMap.RealControl == realControl);
        }
        public ControlMap AddAction(string realControl, string path = "")
        {
            ControlMap act = GetAction(realControl);
            if (act == null)
            {
                act = new(realControl, path);
                controlMaps.Add(act);
            }
            return act;
        }

    }

    public void AddMap(string mameControl, string realControl, string behavior = null)
    {
        ControlMapConfiguration.Maps map;
        if (behavior == null)
            behavior = ControlMapPathDictionary.GetBehavior(realControl);
        map = GetMap(mameControl);
        if (map == null)
        {
            map = new(mameControl, behavior);
            mapList.Add(map);
        }

        string path = ControlMapPathDictionary.GetInputPath(realControl);
        if (string.IsNullOrEmpty(path))
            ConfigManager.WriteConsole($"[ControlMapConfiguration.AddMap] ERROR path unknown action:{mameControl} maped control: {realControl}");
        else
        {
            map.AddAction(realControl, path);
        }
    }

    public void AddMap(string mameControl, string[] realControlsToAssign, string behavior = null)
    {
        foreach (string realControl in realControlsToAssign)
        {
            AddMap(mameControl, realControl, behavior);
        }

        return;
    }

    public void RemoveMaps(string mameControl)
    {
        ControlMapConfiguration.Maps map;
        map = GetMap(mameControl);
        if (map != null)
        {
            mapList.Remove(map);
        }
    }

    public void Merge(ControlMapConfiguration other)
    {
        if (other == null)
        {
            return;
        }

        // Iterate through each map in the other ControlMapConfiguration
        foreach (Maps otherMap in other.mapList)
        {
            // Find a matching map in this ControlMapConfiguration based on the mameControl name
            Maps thisMap = mapList.Find(x => x.mameControl == otherMap.mameControl);

            // If a matching map is found, replace its controlMaps list with the one from the other ControlMapConfiguration
            if (thisMap != null)
            {
                thisMap.controlMaps = otherMap.controlMaps;
            }
            // If a matching map is not found, add the map from the other ControlMapConfiguration to this ControlMapConfiguration
            else
            {
                mapList.Add(otherMap);
            }
        }
    }
    public void SaveAsYaml(string fileName)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        string yaml = serializer.Serialize(this);

        File.WriteAllText(fileName, yaml);
    }
    public static ControlMapConfiguration LoadFromYaml(string fileName)
    {
        if (!File.Exists(fileName))
        {
            return null;
        }
        ConfigManager.WriteConsole($"[ControlMapConfiguration.LoadFromYaml] {fileName}");
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        string yaml = File.ReadAllText(fileName);

        return deserializer.Deserialize<ControlMapConfiguration>(yaml);
    }

    public virtual void Load()
    {
        //to implement
        throw new NotImplementedException("[controlMapConfiguration] Method Load");
    }
    public virtual void Save()
    {
        //to implement
        throw new NotImplementedException("[controlMapConfiguration] Method Save");
    }


    public void ToDebug()
    {
        ConfigManager.WriteConsole("MAME \t Control \t behavior \t Unity Path ");
        foreach (ControlMapConfiguration.Maps m in mapList)
        {
            foreach (ControlMapConfiguration.ControlMap a in m.controlMaps)
            {
                ConfigManager.WriteConsole($"{m.mameControl} \t {a.RealControl} \t {m.behavior} \t {a.Path} ");
            }
        }
    }

    public string AsMarkdown()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("| MAME | Control | Behavior | Unity Path |\n");
        sb.Append("| --- | --- | --- | --- |\n");
        foreach (ControlMapConfiguration.Maps m in mapList)
        {
            foreach (ControlMapConfiguration.ControlMap a in m.controlMaps)
            {
                sb.Append($"| {m.mameControl} | {a.RealControl} | {m.behavior} | `{a.Path}` |\n");
            }
        }
        return sb.ToString();
    }

    public override string ToString()
    {
        return $" ControlMapConfiguration: {mapList.Count}";
    }
}

public class DefaultControlMap : ControlMapConfiguration
{
    static DefaultControlMap instance = null;

    public DefaultControlMap() : base()
    {
        mapList = new();

        //fire with b-button and trigger.
        AddMap("JOYPAD_B", new string[] { "quest-b", "gamepad-b", "quest-right-trigger", "keyboard-enter" });

        AddMap("JOYPAD_A", new string[] { "gamepad-a", "quest-a" });
        AddMap("JOYPAD_X", new string[] { "gamepad-x", "quest-x" });
        AddMap("JOYPAD_Y", new string[] { "gamepad-y", "quest-y" });
        // can'sed.
        AddMap("JOYPAD_START", new string[] { "gamepad-start", "quest-start" });
        AddMap("JOYPAD_SELECT", new string[] { "gamepad-select", "quest-select" });

        AddMap("JOYPAD_UP", new string[] { "quest-left-thumbstick", "gamepad-left-thumbstick" }, "axis");
        AddMap("JOYPAD_DOWN", new string[] { "quest-left-thumbstick", "gamepad-left-thumbstick" }, "axis");
        AddMap("JOYPAD_LEFT", new string[] { "quest-left-thumbstick", "gamepad-left-thumbstick" }, "axis");
        AddMap("JOYPAD_RIGHT", new string[] { "quest-left-thumbstick", "gamepad-left-thumbstick" }, "axis");

        AddMap("JOYPAD_L", new string[] { "quest-left-trigger", "gamepad-left-trigger" });
        AddMap("JOYPAD_R", new string[] { "quest-right-trigger", "gamepad-right-trigger" });

        AddMap("JOYPAD_L2", new string[] { "quest-left-grip", "gamepad-left-bumper" });
        AddMap("JOYPAD_R2", new string[] { "quest-right-grip", "gamepad-right-bumper" });

        // mapped por mame menu in LibretroMameCore
        //AddMap(LibretroMameCore.RETRO_DEVICE_ID_JOYPAD_L3,  new string[] {"quest-left-thumbstick-press", "gamepad-left-thumbstick-press"});
        AddMap("JOYPAD_R3", new string[] { "quest-right-thumbstick-press", "gamepad-right-thumbstick-press" });

        AddMap("EXIT", new string[] { "quest-left-grip", "gamepad-left-bumper", "keyboard-esc" });
        AddMap("INSERT", "gamepad-select");

        AddMap("MOUSE_X", new string[] { "quest-right-thumbstick", "gamepad-right-thumbstick" }, "axis");
        AddMap("MOUSE_Y", new string[] { "quest-right-thumbstick", "gamepad-right-thumbstick" }, "axis");
        AddMap("MOUSE_LEFT", new string[] { "quest-b", "gamepad-b" });
        AddMap("MOUSE_RIGHT", new string[] { "quest-a", "gamepad-a" });
        AddMap("MOUSE_MIDDLE", new string[] { "quest-x", "gamepad-x" });
        AddMap("MOUSE_WHEELUP", new string[] { "quest-left-thumbstick", "gamepad-left-thumbstick" }, "axis");
        AddMap("MOUSE_WHEELDOWN", new string[] { "quest-left-thumbstick", "gamepad-left-thumbstick" }, "axis");
        AddMap("MOUSE_HORIZ_WHEELUP", new string[] { "quest-left-thumbstick", "gamepad-left-thumbstick" }, "axis");
        AddMap("MOUSE_HORIZ_WHEELDOWN", new string[] { "quest-left-thumbstick", "gamepad-left-thumbstick" }, "axis");
        AddMap("MOUSE_BUTTON_4", new string[] { "quest-left-thumbstick-press", "gamepad-left-thumbstick-press" });
        AddMap("MOUSE_BUTTON_5", new string[] { "quest-right-thumbstick-press", "gamepad-right-thumbstick-press" });

    }

    public static DefaultControlMap Instance
    {
        get
        {
            if (instance == null)
                instance = new();
            return instance;
        }
    }
}

public class GlobalControlMap : DefaultControlMap
{
    public GlobalControlMap()
    {
        Load();
    }
    public override void Save()
    {
        SaveAsYaml(ConfigManager.ConfigControllersDir + "/global.yaml");
    }
    public override void Load()
    {
        ControlMapConfiguration global = LoadFromYaml(ConfigManager.ConfigControllersDir + "/global.yaml");
        Merge(global);
    }
}


public class GameControlMap : GlobalControlMap
{
    private string gameFile;
    public GameControlMap(string gameFile) : base()
    {
        this.gameFile = gameFile;
        Load();
    }

    public static string Path(string gameFileName)
    {
        return ConfigManager.ConfigControllersDir + "/" + gameFileName + ".yaml";
    }

    private string getFileName()
    {
        return Path(gameFile);
    }

    public static bool ExistsConfiguration(string gameFileName)
    {
        string path = Path(gameFileName);
        bool exists = File.Exists(Path(gameFileName)); 
        ConfigManager.WriteConsole($"[GameControlMap] configuration exists for game {gameFileName}: {exists}");
        return exists;
    }

    public override void Save()
    {
        SaveAsYaml(getFileName());
    }
    public override void Load()
    {
        ControlMapConfiguration gameConfig = ControlMapConfiguration.LoadFromYaml(getFileName());
        Merge(gameConfig);
    }
}

public class CustomControlMap : GlobalControlMap
{
    public CustomControlMap(ControlMapConfiguration defaultConf) : base()
    {

        Merge(defaultConf);
        ConfigManager.WriteConsole($"[CustomControlMap] Merged {defaultConf.ToString()}");
    }
}
