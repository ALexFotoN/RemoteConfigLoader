using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class RemoteConfigLoader : MonoBehaviour
{
    public enum ConfigFormat { CSV, JSON }

    [Header("Remote config")]
    [SerializeField] private string configUrl;
    [SerializeField] private ConfigFormat format = ConfigFormat.CSV;
    [SerializeField] private string weaponId = "1";

    [Header("Fallback defaults")]
    [SerializeField] private float defaultDamage = 10f;
    [SerializeField] private float defaultCooldown = 1f;

    [SerializeField] private Weapon weapon;

    private string LocalPath =>
        Path.Combine(Application.persistentDataPath, "weapon_config.txt");

    [Serializable]
    private class WeaponConfig
    {
        public string id;
        public float damage;
        public float cooldown;
    }

    [Serializable]
    private class ConfigList
    {
        public WeaponConfig[] items;
    }

    private void Start()
    {
        StartCoroutine(LoadConfig());
    }

    private IEnumerator LoadConfig()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(configUrl))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string text = request.downloadHandler.text;

                if (TryParseConfig(text, out WeaponConfig config))
                {
                    ApplyAndSave(config);
                    yield break;
                }

                Debug.LogError("Remote config is invalid. Trying local copy.");
            }
            else
            {
                Debug.LogError($"Config download failed: {request.error}. Trying local copy.");
            }
        }

        if (File.Exists(LocalPath) &&
            TryParseConfig(File.ReadAllText(LocalPath), out WeaponConfig localConfig))
        {
            Apply(localConfig);
            Debug.Log("Loaded weapon config from local copy.");
            yield break;
        }

        Debug.LogError("No valid local config found. Using default values.");
        Apply(new WeaponConfig
        {
            id = weaponId,
            damage = defaultDamage,
            cooldown = defaultCooldown
        });
    }

    private bool TryParseConfig(string text, out WeaponConfig result)
    {
        result = null;

        try
        {
            WeaponConfig[] configs = format == ConfigFormat.JSON
                ? ParseJson(text)
                : ParseCsv(text);

            foreach (WeaponConfig config in configs)
            {
                if (config.id != weaponId)
                    continue;

                if (config.damage < 0 || config.cooldown <= 0)
                {
                    Debug.LogError($"Invalid values for weapon id '{weaponId}'.");
                    return false;
                }

                result = config;
                return true;
            }

            Debug.LogError($"Weapon id '{weaponId}' was not found in config.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Config parsing failed: {e.Message}");
        }

        return false;
    }

    private WeaponConfig[] ParseJson(string text)
    {
        // Ожидается JSON вида: {"items":[{"id":"1","damage":12,"cooldown":0.5}]}
        ConfigList list = JsonUtility.FromJson<ConfigList>(text);
        if (list == null || list.items == null)
            throw new FormatException("Expected JSON object with an 'items' array.");

        return list.items;
    }

    private WeaponConfig[] ParseCsv(string text)
    {
        string[] lines = text.Replace("\r", "").Split('\n');
        if (lines.Length < 2)
            throw new FormatException("CSV must contain a header and at least one row.");

        string[] headers = lines[0].Split(',');
        int idIndex = Array.IndexOf(headers, "id");
        int damageIndex = Array.IndexOf(headers, "damage");
        int cooldownIndex = Array.IndexOf(headers, "cooldown");
        if (idIndex < 0 || damageIndex < 0 || cooldownIndex < 0)
            throw new FormatException("CSV headers must be id,damage,cooldown.");

        var configs = new System.Collections.Generic.List<WeaponConfig>();

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            string[] values = lines[i].Split(',');
            int maxIndex = Math.Max(idIndex, Math.Max(damageIndex, cooldownIndex));

            if (values.Length <= maxIndex)
                throw new FormatException($"Invalid CSV row {i + 1}.");

            configs.Add(new WeaponConfig
            {
                id = values[idIndex].Trim(),
                damage = ParseNumber(values[damageIndex]),
                cooldown = ParseNumber(values[cooldownIndex])
            });
        }

        return configs.ToArray();
    }

    private float ParseNumber(string value)
    {
        if (!float.TryParse(value.Trim(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out float number))
        {
            throw new FormatException($"Invalid number: '{value}'.");
        }

        return number;
    }

    private void ApplyAndSave(WeaponConfig config)
    {
        Apply(config);

        try
        {
            File.WriteAllText(LocalPath, SerializeForLocal(config));
            Debug.Log($"Config saved to: {LocalPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Could not save local config: {e.Message}");
        }
    }

    private void Apply(WeaponConfig config)
    {
        if (weapon == null)
        {
            Debug.LogError("Weapon reference is not assigned.");
            return;
        }

        weapon.Apply(config.damage, config.cooldown);
    }

    // Локальный файл хранится в JSON независимо от формата удалённого источника.
    private string SerializeForLocal(WeaponConfig config)
    {
        return JsonUtility.ToJson(config);
    }
};