using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MyFirstCoreApp.Extensions;

public static class EnumExtensions
{
    /// <summary>
    /// تبدیل Enum به SelectList با پشتیبانی از Display Attribute
    /// </summary>
    public static List<SelectListItem> GetEnumSelectListWithDisplay<TEnum>()
        where TEnum : struct, Enum
    {
        var list = new List<SelectListItem>();

        foreach (var value in Enum.GetValues<TEnum>())
        {
            var fieldName = value.ToString();
            var field = typeof(TEnum).GetField(fieldName);
            var displayAttr = field?.GetCustomAttribute<DisplayAttribute>();

            list.Add(new SelectListItem
            {
                Value = Convert.ToInt32(value).ToString(),
                Text = displayAttr?.GetName() ?? fieldName
            });
        }

        return list;
    }
}