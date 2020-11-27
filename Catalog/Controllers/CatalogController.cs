using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Catalog.Infastructure;
using Catalog.Model;
using Catalog.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Controllers
{
    [Route("api/v1/[controller]")]
    public class CatalogController : Controller
    {
        private readonly CatalogContext _catalogContext;

        public CatalogController(CatalogContext context)
        {
            _catalogContext = context ?? throw new ArgumentNullException(nameof(context));
            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        [HttpGet]
        [Route("items")]
        [ProducesResponseType(typeof(PaginatedItemsViewModel<CatalogItem>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(IEnumerable<CatalogItem>), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> ItemsAsync([FromQuery] int pageSize = 10, [FromQuery] int pageIndex = 0, string ids = null)
        {
            if (!string.IsNullOrEmpty(ids))
            {
                var items = await GetItemsByIdsAsync(ids);
                if (!items.Any())
                {
                    return BadRequest("");
                }
                return Ok(items);
            }

            var totalItems = await _catalogContext.CatalogItems
                                                  .LongCountAsync();

            var itemsOnPage = await _catalogContext.CatalogItems
                                                   .OrderBy(c => c.Name)
                                                   .Skip(pageSize * pageIndex)
                                                   .Take(pageSize)
                                                   .ToListAsync();

            var model = new PaginatedItemsViewModel<CatalogItem>(pageIndex, pageSize, totalItems, itemsOnPage);

            return Ok(model);
        }

        private async Task<List<CatalogItem>> GetItemsByIdsAsync(string ids)
        {
            var numIds = ids.Split(",").Select(id => (Ok: int.TryParse(id, out int x), Value: x));

            if (!numIds.All(nid => nid.Ok))
            {
                return new List<CatalogItem>();
            }

            var idsToSelect = numIds.Select(id => id.Value);
            var items = await _catalogContext.CatalogItems.Where(ci => idsToSelect.Contains(ci.Id)).ToListAsync();
            return items;
        }

        [HttpGet]
        [Route("items/type/all/{catalogTypeId:int?}")]
        [ProducesResponseType(typeof(PaginatedItemsViewModel<CatalogItem>), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<PaginatedItemsViewModel<CatalogItem>>> ItemsByCatalogTypeIdAsync(int? catalogTypeId, [FromQuery] int pageSize = 10, [FromQuery] int pageIndex = 0)
        {
            var root = (IQueryable<CatalogItem>)_catalogContext.CatalogItems;
            if (catalogTypeId.HasValue)
            {
                root = root.Where(ci => ci.CatalogTypeId == catalogTypeId);
            }

            var totalItems = await root.LongCountAsync();

            var itemsOnPage = await root
                .Skip(pageSize * pageIndex)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedItemsViewModel<CatalogItem>(pageIndex, pageSize, totalItems, itemsOnPage);
        }

        [HttpGet]
        [Route("items/{id:int}")]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(CatalogItem), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<CatalogItem>> ItemByIdAsync(int id)
        {
            if (id <= 0)
            {
                return BadRequest();
            }

            var item = await _catalogContext.CatalogItems.SingleOrDefaultAsync(ci => ci.Id == id);

            if (item != null)
                return item;
            return NotFound();
        }

        [HttpGet]
        [Route("items/withname/{name:minlength(1)}")]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(PaginatedItemsViewModel<CatalogItem>), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<PaginatedItemsViewModel<CatalogItem>>> ItemsByNameAsync(string name, [FromQuery] int pageSize = 10, [FromQuery] int pageIndex = 0)
        {

            var totalItems = await _catalogContext.CatalogItems.Where(ci => ci.Name.StartsWith(name))
                                                               .LongCountAsync();

            var items = await _catalogContext.CatalogItems
                                       .Where(ci => ci.Name.StartsWith(name))
                                       .OrderBy(ci => ci.Name)
                                       .Skip(pageSize * pageIndex)
                                       .Take(pageSize)
                                       .ToListAsync();
            if (items != null)
                return new PaginatedItemsViewModel<CatalogItem>(pageIndex, pageSize, totalItems, items);

            return NotFound();
        }

        [HttpGet]
        [Route("catalogtypes")]
        [ProducesResponseType(typeof(List<CatalogType>), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<List<CatalogType>>> CatalogTypeAsync()
        {
            return await _catalogContext.CatalogTypes.ToListAsync();
        }

        [HttpPut]
        [Route("items")]
        public async Task<ActionResult> UpdateProductAsync([FromBody] CatalogItem productForUpdate)
        {
            var item = await _catalogContext.CatalogItems.SingleOrDefaultAsync(x => x.Id == productForUpdate.Id);
            if (item == null)
                return NotFound(new { Message = $"Item with id {productForUpdate.Id} not found" });
            _catalogContext.CatalogItems.Update(item);
            await _catalogContext.SaveChangesAsync();
            return CreatedAtAction(nameof(ItemByIdAsync), new { id = item.Id }, null);
        }

        [HttpPost]
        [Route("items")]
        [ProducesResponseType((int)HttpStatusCode.Created)]
        public async Task<ActionResult> CreateProductAsync([FromBody] CatalogItem product)
        {
            var item = new CatalogItem()
            {
                CatalogTypeId = product.CatalogTypeId,
                Description = product.Description,
                Name = product.Name,
                Price = product.Price
            };

            _catalogContext.CatalogItems.Add(item);
            await _catalogContext.SaveChangesAsync();
            return CreatedAtAction(nameof(ItemByIdAsync), new { id = item.Id }, null);
        }

        [Route("{id}")]
        [HttpDelete]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<ActionResult> DeleteProductAstnc(int id)
        {
            var item = await _catalogContext.CatalogItems.SingleOrDefaultAsync(x => x.Id == id);
            if (item == null)
                return NotFound();

            _catalogContext.CatalogItems.Remove(item);
            await _catalogContext.SaveChangesAsync();
            return NoContent();
        }
    }
}
