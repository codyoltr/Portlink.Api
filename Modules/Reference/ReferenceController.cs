using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portlink.Api.Data;
using Portlink.Api.DTOs.Common;

namespace Portlink.Api.Modules.Reference;

[ApiController]
public class ReferenceController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReferenceController(AppDbContext db) => _db = db;

    // GET /api/ports?search=izmir&region=Ege
    [HttpGet("api/ports")]
    public async Task<IActionResult> GetPorts([FromQuery] string? search, [FromQuery] string? region,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var query = _db.Ports.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || p.Code.Contains(search));
        if (!string.IsNullOrWhiteSpace(region))
            query = query.Where(p => p.Region != null && p.Region.Contains(region));

        var ports = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PortResponse
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Region = p.Region,
                Coordinates = p.Coordinates
            })
            .ToListAsync();

        return Ok(ApiResponse<List<PortResponse>>.Ok(ports));
    }

    // GET /api/service-categories
    [HttpGet("api/service-categories")]
    public async Task<IActionResult> GetServiceCategories()
    {
        var categories = await _db.ServiceCategories
            .OrderBy(sc => sc.Title)
            .Select(sc => new ServiceCategoryResponse
            {
                Id = sc.Id,
                Code = sc.Code,
                Title = sc.Title,
                SubServices = sc.SubServices != null ? sc.SubServices.ToList() : new List<string>()
            })
            .ToListAsync();

        return Ok(ApiResponse<List<ServiceCategoryResponse>>.Ok(categories));
    }
}
