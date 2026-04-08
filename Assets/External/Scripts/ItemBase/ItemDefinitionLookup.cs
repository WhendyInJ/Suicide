using System.Collections.Generic;
using UnityEngine;

public static class ItemDefinitionLookup
{
    private static readonly Dictionary<string, ItemDefinition> cachedById = new();
    private static bool isInitialized;

    public static ItemDefinition GetById(string itemId)
    {
        EnsureInitialized();

        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        cachedById.TryGetValue(itemId, out ItemDefinition definition);
        return definition;
    }

    public static void Rebuild()
    {
        cachedById.Clear();

        ItemDefinition[] definitions = Resources.LoadAll<ItemDefinition>("ItemDefinitions");
        for (int i = 0; i < definitions.Length; i++)
        {
            ItemDefinition definition = definitions[i];
            if (definition == null)
                continue;

            string id = definition.ItemId;
            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (cachedById.ContainsKey(id))
            {
                Debug.LogWarning($"중복된 ItemId가 있습니다. id={id}, asset={definition.name}");
                cachedById[id] = definition;
                continue;
            }

            cachedById.Add(id, definition);
        }

        isInitialized = true;
    }

    private static void EnsureInitialized()
    {
        if (isInitialized)
            return;

        Rebuild();
    }
}