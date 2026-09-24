using Microsoft.EntityFrameworkCore;
using OMS_Backend.Common.Exceptions;
using OMS_Backend.Data;
using OMS_Backend.DTOs;
using OMS_Backend.Models;

namespace OMS_Backend.Services
{
    public class OrderService : IOrderService
    {
        private readonly OMSDbContext _db;
        private readonly IWebHostEnvironment _env;
        private const int LT_OrderStatus = 1;
        private readonly bool _wooEnabled;

        public OrderService(OMSDbContext db, IWebHostEnvironment env, IConfiguration configuration)
        {
            _db = db;
            _env = env;
            _wooEnabled = WooCommerceSecurity.Enabled(configuration) || configuration.GetValue<bool>("Shopify:Enabled");
        }

        // OWNERSHIP GUARD
        private static void EnsureOwnership(Order order, int userId, bool isCustomer)
        {
            if (isCustomer && order.CustomerId != userId)
                throw new NotFoundException(nameof(Order), order.Id);
        }

        // =====================================================================
        // GET ORDERS (list)
        // =====================================================================
        public async Task<PagedResult<OrderListDto>> GetOrdersAsync(OrderQueryDto q, int userId, bool isCustomer)
        {
            var query = _db.Orders.AsNoTracking().Where(o => !o.IsDeleted);

            if (q.Source == "WooCommerce" || q.Source == "Shopify")
                query = _wooEnabled
                    ? query.Where(o => _db.Set<WooCommerceOrder>().Any(w => w.OrderId == o.Id && w.Connection.Provider == q.Source))
                    : query.Where(o => false);
            else if (q.Source == "Manual" && _wooEnabled)
                query = query.Where(o => !_db.Set<WooCommerceOrder>().Any(w => w.OrderId == o.Id));

            if (isCustomer)
                query = query.Where(o => o.CustomerId == userId);

            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var s = q.Search.Trim();
                query = query.Where(o =>
                    o.ManufacturerOrderNumber.Contains(s) ||
                    (o.CustomerOrderNumber != null && o.CustomerOrderNumber.Contains(s)) ||
                    o.CustomerProductTitle.Contains(s) ||
                    (o.ManufacturerProductTitle != null && o.ManufacturerProductTitle.Contains(s)) ||
                    (o.TrackingNumber != null && o.TrackingNumber.Contains(s)) ||
                    o.ConsigneeName.Contains(s) ||
                    o.Customer.FirstName.Contains(s) ||
                    o.Customer.LastName.Contains(s));
            }

            if (q.StatusId.HasValue) query = query.Where(o => o.OrderStatusId == q.StatusId);
            if (q.PriorityId.HasValue) query = query.Where(o => o.PriorityId == q.PriorityId);

            if (!isCustomer && q.CustomerId.HasValue)
                query = query.Where(o => o.CustomerId == q.CustomerId);

            if (q.GenderId.HasValue) query = query.Where(o => o.GenderId == q.GenderId);
            if (q.MaterialId.HasValue)
                query = query.Where(o => o.CustomerMaterialId == q.MaterialId || o.ManufacturerMaterialId == q.MaterialId);
            if (q.DateFrom.HasValue) query = query.Where(o => o.CreatedDate >= q.DateFrom);
            if (q.DateTo.HasValue) query = query.Where(o => o.CreatedDate <= q.DateTo.Value.AddDays(1));

            query = ApplySort(query, q.SortBy, q.SortDirection);

            var total = await query.CountAsync();
            var pageSize = q.PageSize is > 0 and <= 200 ? q.PageSize : 25;
            var pageNumber = q.PageNumber > 0 ? q.PageNumber : 1;

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new OrderListDto
                {
                    Id = o.Id,
                    ManufacturerOrderNumber = o.ManufacturerOrderNumber,
                    CustomerOrderNumber = o.CustomerOrderNumber,
                    CustomerName = o.Customer.FirstName + " " + o.Customer.LastName,
                    CustomerProductTitle = o.CustomerProductTitle,
                    ManufacturerProductTitle = o.ManufacturerProductTitle,
                    Amount = o.Amount,
                    OrderStatusId = o.OrderStatusId,
                    Status = _db.LookupItems
                        .Where(li => li.Id == o.OrderStatusId)
                        .Select(li => li.Name)
                        .FirstOrDefault() ?? "Unknown",
                    Priority = o.PriorityId != null
                        ? _db.LookupItems.Where(li => li.Id == o.PriorityId).Select(li => li.Name).FirstOrDefault()
                        : null,
                    DaysForMaking = o.DaysForMaking,
                    TrackingNumber = o.TrackingNumber,
                    CreatedDate = o.CreatedDate,

                    // Assignment
                    IsAssigned = o.IsAssigned,
                    AssignedDate = o.AssignedDate,

                    Images = o.OrderImages
                        .Where(i => !i.IsDeleted)
                        .OrderBy(i => i.Id)
                        .Select(i => new OrderImageDto { Id = i.Id, ImageURL = i.ImageURL })
                        .ToList()
                })
                .ToListAsync();

            // Auto-compute days live for assigned orders (in memory to keep EF query simple)
            var nowUtc = DateTime.UtcNow;
            foreach (var item in items)
            {
                if (item.IsAssigned && item.AssignedDate.HasValue)
                {
                    var elapsed = (nowUtc - item.AssignedDate.Value).TotalDays;
                    item.DaysForMaking = elapsed < 0 ? 0 : (int)elapsed;
                }
            }

            if (_wooEnabled && items.Count > 0)
            {
                var ids = items.Select(x => x.Id).ToArray();
                var imported = await _db.Set<WooCommerceOrder>()
                    .AsNoTracking()
                    .Include(x => x.Connection)
                    .Where(x => ids.Contains(x.OrderId))
                    .ToDictionaryAsync(x => x.OrderId);

                foreach (var item in items)
                {
                    if (imported.TryGetValue(item.Id, out var link))
                    {
                        item.Source = link.Connection.Provider;
                        AddStoreImages(item.Images, link);
                    }
                }
            }

            return new PagedResult<OrderListDto>
            {
                Items = items,
                TotalCount = total,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        private static IQueryable<Order> ApplySort(IQueryable<Order> query, string? sortBy, string? dir)
        {
            var desc = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);

            Func<IQueryable<Order>, IOrderedQueryable<Order>> orderFn =
                (sortBy ?? "CreatedDate").ToLowerInvariant() switch
                {
                    "manufacturerordernumber" => q => desc
                        ? q.OrderByDescending(o => o.ManufacturerOrderNumber)
                        : q.OrderBy(o => o.ManufacturerOrderNumber),
                    "customer" => q => desc
                        ? q.OrderByDescending(o => o.Customer.FirstName)
                        : q.OrderBy(o => o.Customer.FirstName),
                    "amount" => q => desc
                        ? q.OrderByDescending(o => o.Amount)
                        : q.OrderBy(o => o.Amount),
                    "status" => q => desc
                        ? q.OrderByDescending(o => o.OrderStatusId)
                        : q.OrderBy(o => o.OrderStatusId),
                    "priority" => q => desc
                        ? q.OrderByDescending(o => o.PriorityId)
                        : q.OrderBy(o => o.PriorityId),
                    "daysformaking" => q => desc
                        ? q.OrderByDescending(o => o.DaysForMaking)
                        : q.OrderBy(o => o.DaysForMaking),
                    _ => q => desc
                        ? q.OrderByDescending(o => o.CreatedDate)
                        : q.OrderBy(o => o.CreatedDate),
                };

            return orderFn(query);
        }

        // =====================================================================
        // GET ONE
        // =====================================================================
        public async Task<OrderDetailsDto> GetOrderByIdAsync(int id, int userId, bool isCustomer)
        {
            var order = await _db.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderImages)
                .Include(o => o.StatusHistories)
                .Include(o => o.InventoryBills)
                .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted)
                ?? throw new NotFoundException(nameof(Order), id);

            EnsureOwnership(order, userId, isCustomer);

            return await MapDetailsAsync(order);
        }

        private async Task<OrderDetailsDto> MapDetailsAsync(Order o)
        {
            var store = _wooEnabled
                ? await _db.Set<WooCommerceOrder>()
                    .AsNoTracking()
                    .Include(x => x.Connection)
                    .SingleOrDefaultAsync(x => x.OrderId == o.Id)
                : null;

            var images = o.OrderImages
                .Where(i => !i.IsDeleted)
                .Select(i => new OrderImageDto { Id = i.Id, ImageURL = i.ImageURL })
                .ToList();

            if (store != null)
                AddStoreImages(images, store);

            var lookupIds = new[] { o.OrderStatusId, o.GenderId, o.CustomerMaterialId, o.ManufacturerMaterialId }
                .Concat(o.PriorityId.HasValue ? new[] { o.PriorityId.Value } : Array.Empty<int>())
                .Concat(o.SizeId.HasValue ? new[] { o.SizeId.Value } : Array.Empty<int>())
                .Concat(o.SizeChartId.HasValue ? new[] { o.SizeChartId.Value } : Array.Empty<int>())
                .Concat(o.StatusHistories.Select(h => h.StatusId))
                .Distinct()
                .ToList();

            var lookupNames = await _db.LookupItems
                .Where(li => lookupIds.Contains(li.Id))
                .ToDictionaryAsync(li => li.Id, li => li.Name);

            string? Name(int? id) =>
                id.HasValue && lookupNames.TryGetValue(id.Value, out var n) ? n : null;

            // Live days for assigned orders
            var liveDays = o.DaysForMaking;
            if (o.IsAssigned && o.AssignedDate.HasValue)
            {
                var elapsed = (DateTime.UtcNow - o.AssignedDate.Value).TotalDays;
                liveDays = elapsed < 0 ? 0 : (int)elapsed;
            }

            return new OrderDetailsDto
            {
                Source = store != null ? store.Connection.Provider : "Manual",
                Id = o.Id,
                ManufacturerOrderNumber = o.ManufacturerOrderNumber,
                CustomerOrderNumber = o.CustomerOrderNumber,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer != null ? $"{o.Customer.FirstName} {o.Customer.LastName}" : "",
                CustomerProductTitle = o.CustomerProductTitle,
                ManufacturerProductTitle = o.ManufacturerProductTitle,
                GenderId = o.GenderId,
                Gender = Name(o.GenderId),
                CustomerMaterialId = o.CustomerMaterialId,
                CustomerMaterial = Name(o.CustomerMaterialId),
                ManufacturerMaterialId = o.ManufacturerMaterialId,
                ManufacturerMaterial = Name(o.ManufacturerMaterialId),
                Amount = o.Amount,
                PriorityId = o.PriorityId,
                Priority = Name(o.PriorityId),
                IsCustomSize = o.IsCustomSize,
                SizeId = o.SizeId,
                Size = Name(o.SizeId),
                SizeChartId = o.SizeChartId,
                SizeChart = Name(o.SizeChartId),
                SizeDetails = o.SizeDetails,
                DaysForMaking = liveDays,
                ConsigneeName = o.ConsigneeName,
                ConsigneeAddress = o.ConsigneeAddress,
                TrackingNumber = o.TrackingNumber,
                NotesByCustomer = o.NotesByCustomer,
                NotesByManufacturer = o.NotesByManufacturer,
                OrderStatusId = o.OrderStatusId,
                Status = Name(o.OrderStatusId) ?? "Unknown",
                CreatedDate = o.CreatedDate,
                UpdatedDate = o.UpdatedDate,

                // Assignment
                IsAssigned = o.IsAssigned,
                AssignedDate = o.AssignedDate,

                Images = images,
                StatusHistory = o.StatusHistories
                    .OrderBy(h => h.CreatedDate)
                    .Select(h => new OrderStatusHistoryDto
                    {
                        Id = h.Id,
                        StatusId = h.StatusId,
                        Status = Name(h.StatusId) ?? "Unknown",
                        CreatedDate = h.CreatedDate
                    }).ToList(),
                InventoryBills = o.InventoryBills
                    .Where(b => !b.IsDeleted)
                    .Select(b => new InventoryBillDto
                    {
                        Id = b.Id,
                        BillNumber = b.BillNumber,
                        BillDetails = b.BillDetails,
                        BillImage = b.BillImage,
                        CreatedDate = b.CreatedDate
                    }).ToList()
            };
        }

        // Store images are read-only references, not uploaded OMS attachments.
        private static void AddStoreImages(List<OrderImageDto> images, WooCommerceOrder link)
        {
            var lines = System.Text.Json.JsonSerializer.Deserialize<List<WooLineDto>>(link.ItemsJson) ?? new();

            foreach (var line in lines.Where(x => link.ExternalLineId == 0 || x.Id == link.ExternalLineId))
                foreach (var url in new[] { line.ImageUrl }.Concat(line.ImageUrls ?? new()).Distinct())
                    if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
                        && uri.Scheme == "https"
                        && string.IsNullOrEmpty(uri.UserInfo)
                        && !images.Any(x => x.ImageURL == url))
                    {
                        images.Add(new OrderImageDto { Id = 0, ImageURL = url });
                    }
        }

        // =====================================================================
        // CREATE
        // =====================================================================
        public async Task<OrderDetailsDto> CreateOrderAsync(CreateOrderDto dto, int userId, bool isCustomer)
        {
            var role = await _db.Users.Where(u => u.Id == userId && !u.IsDeleted)
                .Select(u => u.Role != null ? u.Role.Name : null).FirstOrDefaultAsync();
            if (role is not ("Super Admin" or "Admin" or "Finance")) dto.Amount = null;
            if (role is not ("Super Admin" or "Admin" or "Staff")) dto.TrackingNumber = null;

            if (isCustomer)
            {
                dto.CustomerId = userId;
                dto.ManufacturerProductTitle = null;
                dto.TrackingNumber = null;
                dto.NotesByManufacturer = null;
                dto.ManufacturerMaterialId = dto.CustomerMaterialId;
                // Days auto-computed after assignment � customer cannot set it
                dto.DaysForMaking = null;
            }

            await ValidateReferencesAsync(
                dto.CustomerId, dto.GenderId, dto.CustomerMaterialId,
                dto.ManufacturerMaterialId, dto.PriorityId,
                dto.IsCustomSize, dto.SizeId, dto.SizeChartId, dto.SizeDetails);

            var defaultStatus = await _db.LookupItems
                .Where(li => li.LookupDataTypeId == LT_OrderStatus && !li.IsDeleted)
                .OrderBy(li => li.Id)
                .FirstOrDefaultAsync()
                ?? throw new AppConfigurationException("No OrderStatus lookup values are configured.");

            using var tx = await _db.Database.BeginTransactionAsync();

            var order = new Order
            {
                CustomerProductTitle = dto.CustomerProductTitle,
                ManufacturerProductTitle = dto.ManufacturerProductTitle,
                CustomerOrderNumber = dto.CustomerOrderNumber,
                ManufacturerOrderNumber = await GenerateOrderNumberAsync(),
                CustomerId = dto.CustomerId,
                Amount = dto.Amount,
                GenderId = dto.GenderId,
                CustomerMaterialId = dto.CustomerMaterialId,
                ManufacturerMaterialId = dto.ManufacturerMaterialId,
                IsCustomSize = dto.IsCustomSize,
                SizeDetails = dto.IsCustomSize ? dto.SizeDetails : null,
                SizeId = dto.IsCustomSize ? null : dto.SizeId,
                SizeChartId = dto.IsCustomSize ? null : dto.SizeChartId,
                OrderStatusId = defaultStatus.Id,

                ConsigneeName = dto.ConsigneeName ?? string.Empty,
                ConsigneeAddress = dto.ConsigneeAddress ?? string.Empty,

                TrackingNumber = dto.TrackingNumber,
                NotesByCustomer = dto.NotesByCustomer,
                NotesByManufacturer = dto.NotesByManufacturer,

                // Days start at 0; will be computed live after assignment
                DaysForMaking = dto.DaysForMaking ?? 0,
                PriorityId = dto.PriorityId,

                IsAssigned = false,
                AssignedDate = null,

                IsActive = true,
                CreatedBy = userId,
                CreatedDate = DateTime.UtcNow
            };

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            _db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                StatusId = defaultStatus.Id,
                IsActive = true,
                CreatedBy = userId,
                CreatedDate = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            await tx.CommitAsync();

            return await GetOrderByIdAsync(order.Id, userId, isCustomer);
        }

        private async Task<string> GenerateOrderNumberAsync()
        {
            const string prefix = "AD";
            const int startingSeq = 1001;

            var last = await _db.Orders
                .Where(o => o.ManufacturerOrderNumber.StartsWith(prefix))
                .OrderByDescending(o => o.Id)
                .Select(o => o.ManufacturerOrderNumber)
                .FirstOrDefaultAsync();

            var next = startingSeq;
            if (last != null && int.TryParse(last.Substring(prefix.Length), out var lastSeq))
                next = lastSeq + 1;

            return $"{prefix}{next}";
        }

        // =====================================================================
        // UPDATE
        // =====================================================================
        public async Task<OrderDetailsDto> UpdateOrderAsync(int id, UpdateOrderDto dto, int userId, bool isCustomer)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted)
                ?? throw new NotFoundException(nameof(Order), id);

            EnsureOwnership(order, userId, isCustomer);

            var role = await _db.Users.Where(u => u.Id == userId && !u.IsDeleted)
                .Select(u => u.Role != null ? u.Role.Name : null).FirstOrDefaultAsync();
            // Preserve restricted fields even when a client submits modified values.
            if (role is not ("Super Admin" or "Admin" or "Finance")) dto.Amount = order.Amount;
            if (role is not ("Super Admin" or "Admin" or "Staff")) dto.TrackingNumber = order.TrackingNumber;

            if (_wooEnabled && dto.CustomerId != order.CustomerId &&
                await _db.Set<WooCommerceOrder>().AnyAsync(x => x.OrderId == id))
                throw new ValidationAppException("A store order must remain assigned to its connected store owner.");

            if (isCustomer)
            {
                dto.CustomerId = order.CustomerId;
                dto.ManufacturerProductTitle = order.ManufacturerProductTitle;
                dto.PriorityId = order.PriorityId;
                dto.TrackingNumber = order.TrackingNumber;
                dto.NotesByManufacturer = order.NotesByManufacturer;
                dto.ManufacturerMaterialId = order.ManufacturerMaterialId;
                // Preserve days � never touched by customer
                dto.DaysForMaking = order.DaysForMaking;
            }

            await ValidateReferencesAsync(
                dto.CustomerId, dto.GenderId, dto.CustomerMaterialId,
                dto.ManufacturerMaterialId, dto.PriorityId,
                dto.IsCustomSize, dto.SizeId, dto.SizeChartId, dto.SizeDetails);

            order.CustomerProductTitle = dto.CustomerProductTitle;
            order.ManufacturerProductTitle = dto.ManufacturerProductTitle;
            order.CustomerOrderNumber = dto.CustomerOrderNumber;
            order.CustomerId = dto.CustomerId;
            order.Amount = dto.Amount;
            order.GenderId = dto.GenderId;
            order.CustomerMaterialId = dto.CustomerMaterialId;
            order.ManufacturerMaterialId = dto.ManufacturerMaterialId;
            order.IsCustomSize = dto.IsCustomSize;
            order.SizeDetails = dto.IsCustomSize ? dto.SizeDetails : null;
            order.SizeId = dto.IsCustomSize ? null : dto.SizeId;
            order.SizeChartId = dto.IsCustomSize ? null : dto.SizeChartId;

            order.ConsigneeName = dto.ConsigneeName ?? string.Empty;
            order.ConsigneeAddress = dto.ConsigneeAddress ?? string.Empty;

            order.TrackingNumber = dto.TrackingNumber;
            order.NotesByCustomer = dto.NotesByCustomer;
            order.NotesByManufacturer = dto.NotesByManufacturer;

            // Only overwrite DaysForMaking when explicitly provided (admin edit).
            // For assigned orders, the value shown is live-computed and shouldn't be persisted.
            if (!order.IsAssigned && dto.DaysForMaking.HasValue)
                order.DaysForMaking = dto.DaysForMaking.Value;

            order.PriorityId = dto.PriorityId;
            order.UpdatedBy = userId;
            order.UpdatedDate = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return await GetOrderByIdAsync(id, userId, isCustomer);
        }

        // =====================================================================
        // UPDATE STATUS
        // =====================================================================
        public async Task<OrderDetailsDto> UpdateOrderStatusAsync(int id, UpdateOrderStatusDto dto, int userId, bool isCustomer)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted)
                ?? throw new NotFoundException(nameof(Order), id);

            EnsureOwnership(order, userId, isCustomer);

            var statusExists = await _db.LookupItems
                .AnyAsync(li => li.Id == dto.StatusId && li.LookupDataTypeId == LT_OrderStatus && !li.IsDeleted);
            if (!statusExists) throw new ValidationAppException("Invalid status.");

            using var tx = await _db.Database.BeginTransactionAsync();

            order.OrderStatusId = dto.StatusId;
            order.UpdatedBy = userId;
            order.UpdatedDate = DateTime.UtcNow;

            _db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                StatusId = dto.StatusId,
                IsActive = true,
                CreatedBy = userId,
                CreatedDate = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return await GetOrderByIdAsync(id, userId, isCustomer);
        }

        // =====================================================================
        // ASSIGN ORDERS (bulk) � NEW
        // =====================================================================
        public async Task<AssignOrdersResultDto> AssignOrdersAsync(AssignOrdersDto dto, int userId, bool isCustomer)
        {
            if (dto?.OrderIds == null || dto.OrderIds.Count == 0)
                throw new ValidationAppException("No orders selected for assignment.");

            var ids = dto.OrderIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
                throw new ValidationAppException("No valid orders selected.");

            var orders = await _db.Orders
                .Where(o => ids.Contains(o.Id) && !o.IsDeleted)
                .ToListAsync();

            if (orders.Count == 0)
                throw new ValidationAppException("No matching orders found.");

            var now = DateTime.UtcNow;
            var assignedCount = 0;
            var skippedCount = 0;

            foreach (var order in orders)
            {
                // Customer can only assign their own orders
                if (isCustomer && order.CustomerId != userId)
                {
                    skippedCount++;
                    continue;
                }

                // Skip already assigned
                if (order.IsAssigned)
                {
                    skippedCount++;
                    continue;
                }

                order.IsAssigned = true;
                order.AssignedDate = now;
                order.DaysForMaking = 0; // starts counting from today
                order.UpdatedBy = userId;
                order.UpdatedDate = now;
                assignedCount++;
            }

            if (assignedCount > 0)
                await _db.SaveChangesAsync();

            return new AssignOrdersResultDto
            {
                AssignedCount = assignedCount,
                SkippedCount = skippedCount,
                Message = assignedCount > 0
                    ? $"{assignedCount} order(s) assigned successfully."
                    : "No orders could be assigned."
            };
        }

        // =====================================================================
        // IMAGES
        // =====================================================================
        public async Task<List<OrderImageDto>> AddOrderImagesAsync(int orderId, List<IFormFile> files, int userId, bool isCustomer)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted)
                ?? throw new NotFoundException(nameof(Order), orderId);

            EnsureOwnership(order, userId, isCustomer);

            var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            const long maxSize = 5 * 1024 * 1024;

            var uploadRoot = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "orders", orderId.ToString());
            Directory.CreateDirectory(uploadRoot);

            var result = new List<OrderImageDto>();

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExt.Contains(ext))
                    throw new ValidationAppException($"Unsupported image format: {ext}");
                if (file.Length > maxSize)
                    throw new ValidationAppException($"Image '{file.FileName}' exceeds the 5MB limit.");

                var fileName = $"{Guid.NewGuid()}{ext}";
                var fullPath = Path.Combine(uploadRoot, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                    await file.CopyToAsync(stream);

                var relativeUrl = $"/uploads/orders/{orderId}/{fileName}";

                var image = new OrderImage
                {
                    OrderId = orderId,
                    ImageURL = relativeUrl,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                _db.OrderImages.Add(image);
                await _db.SaveChangesAsync();

                result.Add(new OrderImageDto { Id = image.Id, ImageURL = image.ImageURL });
            }

            return result;
        }

        public async Task DeleteOrderImageAsync(int orderId, int imageId, int userId, bool isCustomer)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted)
                ?? throw new NotFoundException(nameof(Order), orderId);

            EnsureOwnership(order, userId, isCustomer);

            var image = await _db.OrderImages
                .FirstOrDefaultAsync(x => x.Id == imageId && x.OrderId == orderId);

            if (image == null) throw new KeyNotFoundException("Image not found.");

            image.IsDeleted = true;
            image.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        // =====================================================================
        // DELETE ORDER
        // =====================================================================
        public async Task DeleteOrderAsync(int id, int userId, bool isCustomer)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);

            if (order == null) throw new NotFoundException(nameof(Order), id);

            EnsureOwnership(order, userId, isCustomer);

            order.IsDeleted = true;
            order.IsActive = false;
            order.UpdatedDate = DateTime.UtcNow;
            order.UpdatedBy = userId;

            await _db.SaveChangesAsync();
        }

        // =====================================================================
        // INVENTORY BILLS
        // =====================================================================
        public async Task<List<InventoryBillDto>> GetInventoryBillsAsync(int orderId, int userId, bool isCustomer)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted)
                ?? throw new NotFoundException(nameof(Order), orderId);

            EnsureOwnership(order, userId, isCustomer);

            return await _db.InventoryBills
                .Where(b => b.OrderId == orderId && !b.IsDeleted)
                .OrderByDescending(b => b.CreatedDate)
                .Select(b => new InventoryBillDto
                {
                    Id = b.Id,
                    BillNumber = b.BillNumber,
                    BillDetails = b.BillDetails,
                    BillImage = b.BillImage,
                    CreatedDate = b.CreatedDate
                }).ToListAsync();
        }

        public async Task<InventoryBillDto> AddInventoryBillAsync(int orderId, SaveInventoryBillDto dto, int userId, bool isCustomer)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted)
                ?? throw new NotFoundException(nameof(Order), orderId);

            EnsureOwnership(order, userId, isCustomer);

            string? billImageUrl = null;

            if (dto.BillImageFile != null)
            {
                var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };
                const long maxSize = 10 * 1024 * 1024;

                var ext = Path.GetExtension(dto.BillImageFile.FileName).ToLowerInvariant();
                if (!allowedExt.Contains(ext))
                    throw new ValidationAppException($"Unsupported bill image format: {ext}");
                if (dto.BillImageFile.Length > maxSize)
                    throw new ValidationAppException("Bill image exceeds the 10MB limit.");

                var uploadRoot = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "inventory-bills", orderId.ToString());
                Directory.CreateDirectory(uploadRoot);

                var fileName = $"{Guid.NewGuid()}{ext}";
                var fullPath = Path.Combine(uploadRoot, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                    await dto.BillImageFile.CopyToAsync(stream);

                billImageUrl = $"/uploads/inventory-bills/{orderId}/{fileName}";
            }

            var bill = new InventoryBill
            {
                OrderId = orderId,
                BillNumber = dto.BillNumber,
                BillDetails = dto.BillDetails,
                BillImage = billImageUrl,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };
            _db.InventoryBills.Add(bill);
            await _db.SaveChangesAsync();

            return new InventoryBillDto
            {
                Id = bill.Id,
                BillNumber = bill.BillNumber,
                BillDetails = bill.BillDetails,
                BillImage = bill.BillImage,
                CreatedDate = bill.CreatedDate
            };
        }

        public async Task DeleteInventoryBillAsync(int orderId, int billId, int userId, bool isCustomer)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted)
                ?? throw new NotFoundException(nameof(Order), orderId);

            EnsureOwnership(order, userId, isCustomer);

            var bill = await _db.InventoryBills
                .FirstOrDefaultAsync(x => x.Id == billId && x.OrderId == orderId);

            if (bill == null) throw new KeyNotFoundException("Bill not found.");

            bill.IsDeleted = true;
            bill.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        // =====================================================================
        // VALIDATION
        // =====================================================================
        private async Task ValidateReferencesAsync(
            int customerId, int genderId, int customerMaterialId,
            int manufacturerMaterialId, int? priorityId,
            bool isCustomSize, int? sizeId, int? sizeChartId, string? sizeDetails)
        {
            var errors = new Dictionary<string, string[]>();

            if (!await _db.Users.AnyAsync(u => u.Id == customerId && !u.IsDeleted))
                errors["customerId"] = new[] { "Selected customer does not exist." };

            if (!await _db.LookupItems.AnyAsync(li => li.Id == genderId && !li.IsDeleted))
                errors["genderId"] = new[] { "Invalid gender." };

            if (!await _db.LookupItems.AnyAsync(li => li.Id == customerMaterialId && !li.IsDeleted))
                errors["customerMaterialId"] = new[] { "Invalid customer material." };

            if (!await _db.LookupItems.AnyAsync(li => li.Id == manufacturerMaterialId && !li.IsDeleted))
                errors["manufacturerMaterialId"] = new[] { "Invalid manufacturer material." };

            if (priorityId.HasValue && !await _db.LookupItems.AnyAsync(li => li.Id == priorityId && !li.IsDeleted))
                errors["priorityId"] = new[] { "Invalid priority." };

            if (isCustomSize)
            {
                if (string.IsNullOrWhiteSpace(sizeDetails))
                    errors["sizeDetails"] = new[] { "Custom size details are required when Custom Size is selected." };
            }
            else
            {
                if (sizeId.HasValue && !await _db.LookupItems.AnyAsync(li => li.Id == sizeId && !li.IsDeleted))
                    errors["sizeId"] = new[] { "Invalid size." };

                if (sizeChartId.HasValue && !await _db.LookupItems.AnyAsync(li => li.Id == sizeChartId && !li.IsDeleted))
                    errors["sizeChartId"] = new[] { "Invalid size chart." };
            }

            if (errors.Count > 0)
                throw new ValidationAppException(errors);
        }
    }
}
