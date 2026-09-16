using ShiftingGuru.Models;

namespace ShiftingGuru.Data;

/// <summary>
/// The single source of service data for the whole site.
/// Today it is backed by a static list. When services move into PostgreSQL,
/// write an EfServiceCatalog that implements this same interface and swap the
/// registration in Program.cs. No controller or view has to change.
/// </summary>
public interface IServiceCatalog
{
    IReadOnlyList<Service> GetAll();

    Service? GetBySlug(string slug);
}