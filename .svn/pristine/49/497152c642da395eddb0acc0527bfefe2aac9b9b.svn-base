using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MyFirstCoreApp.Extensions;

public static class EnumExtensions
{
    /// <summary>
    /// تبدیل Enum به SelectList با پشتیبانی از Display Attribute
    /// </summary>
    public static List<SelectListItem> GetEnumSelectListWithDisplay<TEnum>() where TEnum : Enum
    {
        var list = new List<SelectListItem>();

        foreach (var value in Enum.GetValues(typeof(TEnum)))
        {
            var field = value.GetType().GetField(value.ToString());
            var displayAttr = field?.GetCustomAttribute<DisplayAttribute>();

            list.Add(new SelectListItem
            {
                Value = ((int)value).ToString(),
                Text = displayAttr?.Name ?? value.ToString()
            });
        }

        return list;
    }
}