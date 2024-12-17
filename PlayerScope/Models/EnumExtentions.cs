using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

namespace PlayerScope.Models
{
    // https://github.com/MidoriKami/KamiLib/blob/master/Extensions/EnumExtensions.cs
    public static class EnumExtensions
    {
        public static Func<CultureInfo?>? GetCultureInfoFunc { get; set; }

        private static ResourceManager ResourceManager { get; } =
            new ResourceManager("PlayerScope.Properties.Loc", Assembly.GetExecutingAssembly());

        public static void RefreshResources()
        {
            ResourceManager.ReleaseAllResources();
        }

        public static string GetDescription(this Enum value)
        {
            var type = value.GetType();
            if (Enum.GetName(type, value) is { } name)
            {
                if (type.GetField(name) is { } field)
                {
                    if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attr)
                    {
                        var cultureInfo = GetCultureInfoFunc?.Invoke();
                        var localizedKey = $"{type.Name}.{name}";
                        var localizedDescription = ResourceManager.GetString(localizedKey, cultureInfo);

                        return localizedDescription ?? attr.Description;
                    }
                }
            }

            return value.ToString();
        }
    }



}
