using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DropInBadAPI.Controllers
{
    [ApiController]
    [Route("api/organizer/skill-levels")]
    [Authorize] // ต้องล็อกอินและเป็นผู้จัดเท่านั้น
    public class OrganizerSkillLevelsController : ControllerBase
    {
        private readonly IOrganizerSkillLevelService _skillLevelService;
        public OrganizerSkillLevelsController(IOrganizerSkillLevelService skillLevelService) { _skillLevelService = skillLevelService; }

        private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<IActionResult> GetMySkillLevels()
        {
            var levels = await _skillLevelService.GetLevelsByOrganizerAsync(GetCurrentUserId());
            return Ok(levels);
        }

        [HttpPost]
        public async Task<IActionResult> SaveMySkillLevels([FromBody] IEnumerable<CreateSkillLevelDto> dtos)
        {
            if (dtos == null || !dtos.Any())
            {
                return BadRequest("Skill level data is required.");
            }

            var savedLevels = await _skillLevelService.SaveLevelsAsync(GetCurrentUserId(), dtos);
            return Ok(savedLevels); // ส่งข้อมูลที่สร้างใหม่ทั้งหมดกลับไป
        }
    }
}