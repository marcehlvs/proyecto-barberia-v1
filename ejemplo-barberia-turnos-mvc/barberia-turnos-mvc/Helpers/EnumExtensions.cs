// Helpers/EnumExtensions.cs
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace barberia_turnos_mvc.Helpers
{
    public static class EnumExtensions
    {
        public static string GetDisplayName(this Enum enumValue)
        {
            var member = enumValue.GetType().GetMember(enumValue.ToString()).FirstOrDefault();
            var attr = member?.GetCustomAttribute<DisplayAttribute>();
            return attr?.Name ?? enumValue.ToString();
        }
    }
}