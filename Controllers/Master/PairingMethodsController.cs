using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PairingMethodsController : ControllerBase
    {
        private readonly IGenericService<PairingMethod> _pairingMethodService;

        public PairingMethodsController(IGenericService<PairingMethod> pairingMethodService)
        {
            _pairingMethodService = pairingMethodService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _pairingMethodService.GetAllAsync());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var pairingMethod = await _pairingMethodService.GetByIdAsync(id);
            if (pairingMethod == null) return NotFound();
            return Ok(pairingMethod);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PairingMethod pairingMethod)
        {
            var newPairingMethod = await _pairingMethodService.AddAsync(pairingMethod);
            return CreatedAtAction(nameof(GetById), new { id = newPairingMethod.PairingMethodId }, newPairingMethod);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] PairingMethod pairingMethod)
        {
            var updatedPairingMethod = await _pairingMethodService.UpdateAsync(id, pairingMethod);
            if (updatedPairingMethod == null) return NotFound();
            return Ok(updatedPairingMethod);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _pairingMethodService.DeleteAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}