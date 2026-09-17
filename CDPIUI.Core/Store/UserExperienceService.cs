using CDPIUI.Core.Store;
using CDPIUI.Core.Store.Data;
using CDPIUI.Core.Store.Database;
using CDPIUI.Core.Store.ViewModels;
using CDPIUI.Shared.Extentions;
using System.Linq;

namespace CDPIUI.Core
{
    internal interface IUserExperienceService
    {
        /// <summary>
        /// Ger requirements for ItemId
        /// </summary>
        /// <param name="storeId">Target Item Id</param>
        /// <returns>List of requirements if exist, otherwise <see cref="null"/> </returns>
        List<Tuple<string, string>>? GetItemRequiredItemsById(string storeId);


        /// <summary>
        /// Get similar items for ItemId
        /// </summary>
        /// <param name="storeId">Target Item Id, to search similar with</param>
        /// <returns>List of similar items if available, otherwise empty list</returns>
        List<RepoItemModel> GetSimilarItemsForStoreId(string storeId);
    }

    internal class UserExperienceService : IUserExperienceService
    {
        private readonly StoreHelper Store;
        public UserExperienceService(StoreHelper store) { Store = store; }

        public List<Tuple<string, string>>? GetItemRequiredItemsById(string storeId)
        {
            if (!DatabaseHelper.Instance.IsItemInstalled(storeId))
                return null;

            List<Tuple<string, string>> requiredItems = [];

            DatabaseStoreItem item = DatabaseHelper.Instance.GetItemById(storeId);
            requiredItems = item.RequiredItemIds;

            return requiredItems;
        }

        public List<RepoItemModel> GetSimilarItemsForStoreId(string storeId)
        {
            List<RepoItemModel> items = [];
            RepoItemModel item = Store.GetItemInfoFromStoreId(storeId);

            foreach (RepoItemModel itemToCheck in Store.ItemsList)
            {
                if (itemToCheck.store_id is null || itemToCheck.dependencies is null) continue;

                if (itemToCheck.store_id == storeId) continue;

                if (itemToCheck.type != "configlist") continue;
                items.AddRange(
                    itemToCheck.dependencies
                    .Where(depensety => depensety.Length > 0 && depensety[0] == storeId && !DatabaseHelper.Instance.IsItemInstalled(itemToCheck.store_id))
                    .Select(depensety => itemToCheck));
            }

            if (!DatabaseHelper.Instance.IsItemInstalled(HardcodedItemIds.AddOnsIds.FirstOrDefault(x => x.Key == AddOns.GoodCheck).Value) &&
                HardcodedItemIds.GoodCheckSupportedComponents.Contains(HardcodedItemIds.ComponentIds.GetKeyByValue(storeId)))
            {
                items.Add(Store.GetItemInfoFromStoreId(HardcodedItemIds.AddOnsIds.FirstOrDefault(x => x.Key == AddOns.GoodCheck).Value));
            }

            return items;
        }
    }
}