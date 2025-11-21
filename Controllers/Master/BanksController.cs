using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BanksController : ControllerBase
    {
        private readonly IGenericService<Bank> _bankService;

        public BanksController(IGenericService<Bank> bankService)
        {
            _bankService = bankService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _bankService.GetAllAsync());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var bank = await _bankService.GetByIdAsync(id);
            if (bank == null) return NotFound();
            return Ok(bank);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Bank bank)
        {
            var newBank = await _bankService.AddAsync(bank);
            return CreatedAtAction(nameof(GetById), new { id = newBank.BankId }, newBank);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Bank bank)
        {
            var updatedBank = await _bankService.UpdateAsync(id, bank);
            if (updatedBank == null) return NotFound();
            return Ok(updatedBank);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _bankService.DeleteAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}