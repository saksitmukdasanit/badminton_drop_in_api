using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace DropInBadAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameTypesController : ControllerBase
    {
        private readonly IGenericService<GameType> _gameTypesService;

        public GameTypesController(IGenericService<GameType> gameTypesService)
        {
            _gameTypesService = gameTypesService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _gameTypesService.GetAllAsync());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var gameTypes = await _gameTypesService.GetByIdAsync(id);
            if (gameTypes == null) return NotFound();
            return Ok(gameTypes);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] GameType gameTypes)
        {
            var newGameType = await _gameTypesService.AddAsync(gameTypes);
            return CreatedAtAction(nameof(GetById), new { id = newGameType.GameTypeId }, newGameType);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] GameType gameTypes)
        {
            var updatedGameType = await _gameTypesService.UpdateAsync(id, gameTypes);
            if (updatedGameType == null) return NotFound();
            return Ok(updatedGameType);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _gameTypesService.DeleteAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}