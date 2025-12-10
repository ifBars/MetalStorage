using MelonLoader;
using MetalStorage.Utils;
using S1API.Building;
using S1API.Internal.Utils;
using S1API.Items;
using S1API.Rendering;
using S1API.Shops;
using S1API.Storage;
using UnityEngine;
using System.Reflection;

namespace MetalStorage.Features
{
    public static class MetalStorageRackCreator
    {
        private static readonly Color MetalColor = new Color(0.5f, 0.5f, 0.55f, 1f);

        public static readonly HashSet<string> MetalItemIds = new HashSet<string>();
        private static readonly Dictionary<string, Sprite> _loadedIcons = new Dictionary<string, Sprite>();

        public static void CreateAllMetalRacks()
        {
            LoadIcons();

            // Register BuildEvents for material customization (replaces Harmony patches)
            BuildEvents.OnGridItemCreated += OnItemBuilt;
            BuildEvents.OnSurfaceItemCreated += OnItemBuilt;
            BuildEvents.OnBuildableItemInitialized += OnItemBuilt;

            // Register StorageEvents for slot expansion (replaces Harmony patches)
            StorageEvents.OnStorageCreated += OnStorageCreated;
            StorageEvents.OnStorageLoading += OnStorageLoading;

            // Create metal variants using S1API
            CreateMetalStorageRack(Constants.StorageRacks.SMALL, "Small Metal Storage Rack", Constants.ItemIds.SMALL, "MetalStorageRack_Small-Icon");
            CreateMetalStorageRack(Constants.StorageRacks.MEDIUM, "Medium Metal Storage Rack", Constants.ItemIds.MEDIUM, "MetalStorageRack_1.5x0.5-Icon");
            CreateMetalStorageRack(Constants.StorageRacks.LARGE, "Large Metal Storage Rack", Constants.ItemIds.LARGE, "MetalStorageRack_Large-Icon");

            MelonLogger.Msg($"Created {MetalItemIds.Count} metal storage rack variants");
        }

        private static void LoadIcons()
        {
            string[] iconNames = {
                "MetalStorageRack_Small-Icon",
                "MetalStorageRack_1.5x0.5-Icon",
                "MetalStorageRack_Large-Icon"
            };

            var assembly = Assembly.GetExecutingAssembly();
            foreach (var iconName in iconNames)
            {
                string resourceName = $"MetalStorage.Assets.{iconName}.png";
                var sprite = ImageUtils.LoadImageFromResource(assembly, resourceName);
                if (sprite != null)
                {
                    _loadedIcons[iconName] = sprite;
                }
            }
        }

        private static void CreateMetalStorageRack(string originalId, string newName, string newId, string iconName)
        {
            try
            {
                var icon = _loadedIcons.TryGetValue(iconName, out var sprite) ? sprite : null;
                var price = Core.GetPrice(newId);

                // Use S1API BuildableItemCreator with CloneFrom - eliminates manual property copying
                var metalRack = BuildableItemCreator.CloneFrom(originalId)
                    .WithBasicInfo(newId, newName, "A metal version of the storage rack. More industrial looking.")
                    .WithIcon(icon)
                    .WithPricing(price, 0.5f)
                    .WithBuildSound(BuildSoundType.Metal)
                    .WithLabelColor(Color.white)
                    .Build();

                MetalItemIds.Add(newId);
                MelonLogger.Msg($"Created {newName} (ID: {newId})");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to create metal storage rack '{newId}': {ex.Message}");
            }
        }

        public static void AddToShops()
        {
            int totalAdded = 0;

            // Use S1API ShopManager to automatically add items to compatible shops
            foreach (string itemId in MetalItemIds)
            {
                var item = ItemManager.GetItemDefinition(itemId);
                if (item == null)
                {
                    MelonLogger.Warning($"Could not find item definition for '{itemId}'");
                    continue;
                }

                // S1API automatically finds shops that sell the same category and adds the item with UI
                int addedCount = ShopManager.AddToCompatibleShops(item);
                totalAdded += addedCount;
            }

            MelonLogger.Msg($"Added metal storage racks to {totalAdded} shop listing(s)");
        }

        /// <summary>
        /// BuildEvents handler for customizing placed items.
        /// Handles material customization only.
        /// Replaces CreateGridItem/CreateSurfaceItem/InitializeBuildableItem Harmony patches.
        /// </summary>
        private static void OnItemBuilt(BuildEventArgs args)
        {
            // Only process our metal items
            if (args == null || !MetalItemIds.Contains(args.ItemId))
                return;

            // Use S1API MaterialHelper to customize appearance
            MaterialHelper.ReplaceMaterials(
                args.GameObject,
                mat => mat.name.ToLower().Contains("brownwood"),
                mat =>
                {
                    // Remove all textures and apply metallic properties
                    MaterialHelper.RemoveAllTextures(mat);
                    MaterialHelper.SetColor(mat, "_BaseColor", MetalColor);
                    MaterialHelper.SetColor(mat, "_Color", MetalColor);
                    MaterialHelper.SetFloat(mat, "_Metallic", 0.8f);
                    MaterialHelper.SetFloat(mat, "_Smoothness", 0.5f);
                    MaterialHelper.SetFloat(mat, "_Glossiness", 0.5f);
                }
            );
        }

        /// <summary>
        /// StorageEvents handler for expanding slots when storage is placed.
        /// Replaces PlaceableStorageEntity_Start_Postfix Harmony patch.
        /// </summary>
        private static void OnStorageCreated(StorageEventArgs args)
        {
            // Only process our metal items
            if (args?.Storage == null || !MetalItemIds.Contains(args.ItemId))
                return;

            int extraSlots = Core.GetExtraSlots(args.ItemId);
            if (extraSlots <= 0)
                return;

            // Use S1API to safely add slots (handles runtime differences internally)
            bool success = args.Storage.AddSlots(extraSlots);

            if (success)
            {
                MelonLogger.Msg($"Expanded {args.ItemId} storage to {args.Storage.SlotCount} slots");
            }
            else
            {
                MelonLogger.Warning($"Failed to expand {args.ItemId} storage");
            }
        }

        /// <summary>
        /// StorageEvents handler for expanding slots when loading from save.
        /// Replaces ItemSet_LoadTo_Prefix Harmony patch.
        /// </summary>
        private static void OnStorageLoading(StorageLoadingEventArgs args)
        {
            // Only process our metal items
            if (args?.Storage == null || !MetalItemIds.Contains(args.ItemId))
                return;

            if (!args.NeedsMoreSlots)
                return; // Save file has fewer items than current slots

            // Expand to fit saved items
            bool success = args.Storage.AddSlots(args.AdditionalSlotsNeeded);

            if (success)
            {
                MelonLogger.Msg($"Expanded {args.ItemId} storage to {args.Storage.SlotCount} slots for save loading");
            }
            else
            {
                MelonLogger.Warning($"Failed to expand {args.ItemId} storage for save loading");
            }
        }
    }
}
