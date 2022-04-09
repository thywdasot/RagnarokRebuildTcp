﻿using System.Globalization;
using System.Reflection;
using CsvHelper;
using RoRebuildServer.Data.CsvDataTypes;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Data.Player;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.Logging;
using RoRebuildServer.ScriptSystem;

namespace RoRebuildServer.Data;

internal class DataLoader
{
    public List<MapEntry> LoadMaps()
    {
        using var tr = new StreamReader(@"ServerData/Db/Maps.csv") as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.CurrentCulture);

        var maps = csv.GetRecords<MapEntry>().ToList();

        ServerLogger.Log($"Loading maps: {maps.Count}");

        return maps;
    }

    public List<InstanceEntry> LoadInstances()
    {
        using var tr = new StreamReader(@"ServerData/Db/Instances.csv") as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.CurrentCulture);

        var instances = new List<InstanceEntry>();

        csv.Read(); //skip header row, we aren't using it

        while (csv.Read())
        {
            var instance = new InstanceEntry();
            instance.Name = csv.Context.Parser.Record.ElementAt(0);
            instance.IsWorldInstance = csv.Context.Parser.Record.ElementAt(1) == "true";
            instance.Maps = csv.Context.Parser.Record.Skip(2).ToList();
            instances.Add(instance);
        }

        return instances;
    }
    
    public ExpChart LoadExpChart()
    {
        using var tr = new StreamReader(@"ServerData/Db/ExpChart.csv") as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.CurrentCulture);

        var entries = csv.GetRecords<CsvExpChart>().ToList();

        var chart = new ExpChart();
        chart.ExpRequired = new int[100];
        chart.ExpRequired[0] = 0; //should always be true but why not!

        foreach (var e in entries)
        {
            chart.ExpRequired[e.Level] = e.Experience;
        }

        return chart;
    }

    public List<MonsterDatabaseInfo> LoadMonsterStats()
    {
        using var tr = new StreamReader(@"ServerData/Db/Monsters.csv") as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.CurrentCulture);

        var monsters = csv.GetRecords<CsvMonsterData>().ToList();

        var obj = new List<MonsterDatabaseInfo>(monsters.Count);

        foreach (var monster in monsters)
        {
            if (monster.Id <= 0)
                continue;

            obj.Add(new MonsterDatabaseInfo()
            {
                Id = monster.Id,
                Code = monster.Code,
                Level = monster.Level,
                HP = monster.HP,
                Exp = monster.Exp,
                Def = monster.Def,
                Vit = monster.Vit,
                Range = monster.Range > 0 ? monster.Range + 1 : 1,
                ScanDist = monster.ScanDist,
                ChaseDist = monster.ChaseDist,
                AtkMin = monster.AtkMin,
                AtkMax = monster.AtkMax,
                AttackTime = monster.AttackTime / 1000f,
                HitTime = monster.HitTime / 1000f,
                RechargeTime = monster.RechargeTime / 1000f,
                MoveSpeed = monster.MoveSpeed / 1000f,
                SpriteAttackTiming = monster.SpriteAttackTiming / 1000f,
                AiType = (MonsterAiType)Enum.Parse(typeof(MonsterAiType), monster.MonsterAi),
                Name = monster.Name
            });
        }

        ServerLogger.Log($"Loading monsters: {obj.Count}");

        using var tr2 = new StreamReader(@"ServerData/Db/Npcs.csv") as TextReader;
        using var csv2 = new CsvReader(tr2, CultureInfo.CurrentCulture);

        var npcCount = 0;
        var npcs = csv2.GetRecords<CsvNpc>().ToList();

        foreach (var npc in npcs)
        {
            obj.Add(new MonsterDatabaseInfo()
            {
                Id = npc.Id,
                Code = npc.Code,
                Name = npc.Name,
                AiType = MonsterAiType.AiEmpty
            });
            npcCount++;
        }


        ServerLogger.Log($"Loading npc types: {npcCount}");

        return obj;
    }
    
    public Dictionary<string, string> LoadServerConfig()
    {
        var config = new Dictionary<string, string>();

        using var tr = new StreamReader(@"ServerData/Db/ServerSettings.csv") as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.CurrentCulture);

        var entries = csv.GetRecords<CsvServerConfig>().ToList();

        foreach (var entry in entries)
        {
            config.Add(entry.Key, entry.Value);
        }

        return config;
    }

    public Assembly CompileScripts()
    {
        try
        {
            var compiler = new ScriptCompiler();

            ServerLogger.Log("Compiling server side scripts...");

            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServerData/Script/");
            foreach (var file in Directory.GetFiles(path, "*.txt", SearchOption.AllDirectories))
            {
                ServerLogger.Debug("Compiling script " + Path.GetRelativePath(path, file));
                compiler.Compile(file);
            }

            ServerLogger.Log("Generating and loading script assembly...");

            var assembly = compiler.Load();

            ServerLogger.Log("Server scripts loaded!");

            return assembly;

            
        }
        catch (Exception ex)
        {
            ServerLogger.LogError("Failed to compile scripts due to exeption: " + ex);
            throw;
        }
    }

    public void LoadNpcScripts(Assembly assembly)
    {
        var npcType = typeof(INpcLoader);
        foreach (var type in assembly.GetTypes().Where(t => t.IsAssignableTo(npcType)))
        {
            var handler = (INpcLoader)Activator.CreateInstance(type)!;
            handler.Load();
        }
    }

    public Dictionary<string, Action<ServerMapConfig>> LoadMapConfigs(Assembly assembly)
    {
        var configs = new Dictionary<string, Action<ServerMapConfig>>();

        var attr = typeof(ServerMapConfigAttribute);
        var types = assembly.GetTypes().SelectMany(t => t.GetMethods())
            .Where(m => m.GetCustomAttributes(attr, false).Length > 0).ToArray();

        foreach (var type in types)
        {
            var action = (Action<ServerMapConfig>)type.CreateDelegate(typeof(Action<ServerMapConfig>));
            var at = type.GetCustomAttribute<ServerMapConfigAttribute>();

            configs.Add(at.MapName, action);
        }

        return configs;
    }

    public List<List<MonsterAiEntry>> LoadAiStateMachines()
    {
        var aiTypeCount = Enum.GetNames(typeof(MonsterAiType)).Length;
        var entryList = new List<List<MonsterAiEntry>>(aiTypeCount);
        for (var i = 0; i < aiTypeCount; i++)
            entryList.Add(new List<MonsterAiEntry>());

        using var tr = new StreamReader(@"ServerData/Db/MonsterAI.csv") as TextReader;
        using var csv = new CsvReader(tr, CultureInfo.CurrentCulture);

        var states = csv.GetRecords<CsvMonsterAI>().ToList();

        foreach (var entry in states)
        {
            var hasError = false;
            hasError |= !Enum.TryParse(entry.AiType, out MonsterAiType aiType);
            hasError |= !Enum.TryParse(entry.State, out MonsterAiState inState);
            hasError |= !Enum.TryParse(entry.InputCheck, out MonsterInputCheck inCheck);
            hasError |= !Enum.TryParse(entry.OutputCheck, out MonsterOutputCheck outCheck);
            hasError |= !Enum.TryParse(entry.EndState, out MonsterAiState outState);

            if (hasError)
                throw new Exception($"Could not parse Ai States: {entry.AiType},{entry.State},{entry.InputCheck},{entry.OutputCheck},{entry.EndState}");

            entryList[(int)aiType].Add(new MonsterAiEntry()
            {
                InputState = inState,
                InputCheck = inCheck,
                OutputCheck = outCheck,
                OutputState = outState
            });
        }

        return entryList;
    }
}