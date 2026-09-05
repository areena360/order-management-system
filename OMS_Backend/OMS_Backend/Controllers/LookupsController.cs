using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OMS_Backend.Data;
using OMS_Backend.DTOs;

namespace OMS_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LookupsController : ControllerBase
    {
        private readonly OMSDbContext _db;
        public LookupsController(OMSDbContext db) => _db = db;

        // GET api/lookups/types
        // Returns all lookup types with names, e.g. { OrderStatus: 1, Priority: 2, Gender: 3, Material: 4, Size: 5, SizeChart: 6 }
        [HttpGet("types")]
        public async Task<IActionResult> GetTypes()
        {
            var types = await _db.LookupTypes
                .Where(t => !t.IsDeleted)
                .Select(t => new { t.Id, t.Name })
                .ToListAsync();
            return Ok(types);
        }

        // GET api/lookups/by-type/{typeId}
        [HttpGet("by-type/{typeId}")]
        public async Task<IActionResult> GetByType(int typeId)
        {
            var items = await _db.LookupItems
                .Where(li => li.LookupDataTypeId == typeId && !li.IsDeleted && li.IsActive)
                .OrderBy(li => li.Id)
                .Select(li => new LookupItemDto { Id = li.Id, Name = li.Name })
                .ToListAsync();
            return Ok(items);
        }

        // GET api/lookups/customers - simple searchable customer list for Order form
        [HttpGet("customers")]
        public async Task<IActionResult> GetCustomers([FromQuery] string? search)
        {
            var query = _db.Users.Where(u => !u.IsDeleted && u.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u => u.FirstName.Contains(search) || u.LastName.Contains(search) || u.Email.Contains(search));

            var customers = await query
                .OrderBy(u => u.FirstName)
                .Take(50)
                .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName, u.Email })
                .ToListAsync();

            return Ok(customers);
        }
    }
}
