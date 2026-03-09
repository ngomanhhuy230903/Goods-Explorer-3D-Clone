// OrderQueueExtension.cs
// IOrderTrayProvider đã được định nghĩa trong OrderQueue.cs → file này chỉ giữ extension method.

using System.Collections.Generic;
using UnityEngine;
using FoodMatch.Core;

namespace FoodMatch.Order
{
    public static class OrderQueueExtensions
    {
        public static MatchResult TryMatchFoodWithReservation(
            this IOrderTrayProvider provider, int foodID, int foodInstanceId)
        {
            var activeTrays = provider.GetActiveTrays();
            if (activeTrays == null) return MatchResult.NoMatch();

            foreach (var tray in activeTrays)
            {
                if (tray == null) continue;
                if (tray.CurrentStateId != OrderTrayStateId.Active) continue;

                if (tray.TryMatchAndReserve(foodID, foodInstanceId, out int slotIndex))
                    return MatchResult.Matched(tray, slotIndex);
            }

            return MatchResult.NoMatch();
        }

        public static MatchResult TryMatchFoodWithReservation(
            this OrderQueue queue, int foodID, int foodInstanceId)
        {
            if (queue is IOrderTrayProvider provider)
                return TryMatchFoodWithReservation(provider, foodID, foodInstanceId);

            Debug.LogError("[OrderQueueExtensions] OrderQueue chưa implement IOrderTrayProvider!");
            return MatchResult.NoMatch();
        }
    }
}