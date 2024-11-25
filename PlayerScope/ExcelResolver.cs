using Dalamud;
using Dalamud.Game;
using Lumina.Excel;

namespace PlayerScope;

// From https://github.com/LittleNightmare/UsedName/blob/Memory/UsedName/ExcelResolver.cs
public class ExcelResolver<T> where T : struct, Lumina.Excel.IExcelRow<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExcelResolver{T}"/> class.
    /// </summary>
    /// <param name="id">The ID of the classJob.</param>
    internal ExcelResolver(uint id)
    {
        this.Id = id;
    }

    /// <summary>
    /// Gets the ID to be resolved.
    /// </summary>
    public uint Id { get; }

    /// <summary>
    /// Gets GameData linked to this excel row.
    /// </summary>
    public T? GameData => Plugin.DataManager.GetExcelSheet<T>()?.GetRow(this.Id);

    /// <summary>
    /// Gets GameData linked to this excel row with the specified language.
    /// </summary>
    /// <param name="language">The language.</param>
    /// <returns>The ExcelRow in the specified language.</returns>
    public T? GetWithLanguage(ClientLanguage language) => Plugin.DataManager.GetExcelSheet<T>(language)?.GetRow(this.Id);
}