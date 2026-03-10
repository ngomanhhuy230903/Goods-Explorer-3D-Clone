using System;

namespace FoodMatch.Items
{
    /// <summary>
    /// Đánh dấu class là Booster để BoosterManager tự động scan và đăng ký.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class BoosterAttribute : Attribute { }
}