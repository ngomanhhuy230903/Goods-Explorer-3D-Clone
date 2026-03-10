using System;
using UnityEngine;
using FoodMatch.Food;
using FoodMatch.Order;
using FoodMatch.Tray;

namespace FoodMatch.Core
{
    public enum DeliveryCommandType { ToOrder, ToBackup }
    public enum DeliveryCommandStatus { Pending, Executing, Completed, Cancelled, Failed }

    /// <summary>
    /// Interface Command chuẩn — mọi delivery đều implement.
    /// </summary>
    public interface IDeliveryCommand
    {
        DeliveryCommandType Type { get; }
        DeliveryCommandStatus Status { get; }
        FoodItem Food { get; }
        void Execute(Action onDone);
        void Cancel();
    }


    public sealed class OrderDeliveryCommand : IDeliveryCommand
    {
        public DeliveryCommandType Type => DeliveryCommandType.ToOrder;
        public DeliveryCommandStatus Status { get; private set; } = DeliveryCommandStatus.Pending;
        public FoodItem Food { get; }

        public readonly OrderTray TargetTray;
        public readonly int SlotIndex;
        private readonly int _trayId;

        public OrderDeliveryCommand(FoodItem food, OrderTray tray, int slotIndex)
        {
            Food = food;
            TargetTray = tray;
            SlotIndex = slotIndex;
            _trayId = tray.TrayIndex;
        }

        public void Execute(Action onDone)
        {
            if (Status == DeliveryCommandStatus.Cancelled)
            {
                onDone?.Invoke();
                return;
            }

            Status = DeliveryCommandStatus.Executing;
        }

        public void MarkCompleted()
        {
            Status = DeliveryCommandStatus.Completed;
            SlotReservationRegistry.Instance.ReleaseOrderSlot(_trayId, SlotIndex);
        }

        public void MarkFailed()
        {
            Status = DeliveryCommandStatus.Failed;
            SlotReservationRegistry.Instance.ReleaseOrderSlot(_trayId, SlotIndex);
        }

        public void Cancel()
        {
            Status = DeliveryCommandStatus.Cancelled;
            SlotReservationRegistry.Instance.ReleaseOrderSlot(_trayId, SlotIndex);
        }
    }

    // ─── Concrete Command: bay xuống BackupTray ───────────────────────────────

    public sealed class BackupDeliveryCommand : IDeliveryCommand
    {
        public DeliveryCommandType Type => DeliveryCommandType.ToBackup;
        public DeliveryCommandStatus Status { get; private set; } = DeliveryCommandStatus.Pending;
        public FoodItem Food { get; }

        public readonly int SlotIndex;

        public BackupDeliveryCommand(FoodItem food, int slotIndex)
        {
            Food = food;
            SlotIndex = slotIndex;
        }

        public void Execute(Action onDone)
        {
            if (Status == DeliveryCommandStatus.Cancelled)
            {
                onDone?.Invoke();
                return;
            }
            Status = DeliveryCommandStatus.Executing;
        }

        public void MarkCompleted()
        {
            Status = DeliveryCommandStatus.Completed;
            SlotReservationRegistry.Instance.ReleaseBackupSlot(SlotIndex);
        }

        public void MarkFailed()
        {
            Status = DeliveryCommandStatus.Failed;
            SlotReservationRegistry.Instance.ReleaseBackupSlot(SlotIndex);
        }

        public void Cancel()
        {
            Status = DeliveryCommandStatus.Cancelled;
            SlotReservationRegistry.Instance.ReleaseBackupSlot(SlotIndex);
        }
    }
}