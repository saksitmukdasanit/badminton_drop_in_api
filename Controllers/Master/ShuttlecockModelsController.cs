using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShuttlecockModelsController : ControllerBase
    {
        private readonly IGenericService<ShuttlecockModel> _shuttlecockModelService;

        public ShuttlecockModelsController(IGenericService<ShuttlecockModel> shuttlecockModelService)
        {
            _shuttlecockModelService = shuttlecockModelService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _shuttlecockModelService.GetAllAsync());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var shuttlecockModel = await _shuttlecockModelService.GetByIdAsync(id);
            if (shuttlecockModel == null) return NotFound();
            return Ok(shuttlecockModel);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ShuttlecockModel shuttlecockModel)
        {
            var newShuttlecockModel = await _shuttlecockModelService.AddAsync(shuttlecockModel);
            return CreatedAtAction(nameof(GetById), new { id = newShuttlecockModel.ModelId }, newShuttlecockModel);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ShuttlecockModel shuttlecockModel)
        {
            var updatedShuttlecockModel = await _shuttlecockModelService.UpdateAsync(id, shuttlecockModel);
            if (updatedShuttlecockModel == null) return NotFound();
            return Ok(updatedShuttlecockModel);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _shuttlecockModelService.DeleteAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}