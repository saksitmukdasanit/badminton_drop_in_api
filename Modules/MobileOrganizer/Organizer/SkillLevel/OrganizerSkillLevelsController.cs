using DropInBadAPI.Dtos;
using DropInBadAPI.Models; // << เพิ่ม using สำหรับ Response<T>
using DropInBadAPI.Service.Mobile.Organizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DropInBadAPI.Controllers.Mobile
{
    [ApiController]
    [Route("api/organizer/skill-levels")]
    [Authorize]
    public class OrganizerSkillLevelsController : ControllerBase
    {
        private readonly IOrganizerSkillLevelService _skillLevelService;
        public OrganizerSkillLevelsController(IOrganizerSkillLevelService skillLevelService) { _skillLevelService = skillLevelService; }

        private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<ActionResult<Response<IEnumerable<SkillLevelDto>>>> GetMySkillLevels()
        {
            var levels = await _skillLevelService.GetLevelsByOrganizerAsync(GetCurrentUserId());
            return Ok(new Response<IEnumerable<SkillLevelDto>> 
            { 
                Status = 200, 
                Message = "Skill levels retrieved successfully.", 
                Data = levels 
            });
        }

        [HttpPost]
        public async Task<ActionResult<Response<IEnumerable<SkillLevelDto>>>> SaveMySkillLevels([FromBody] IEnumerable<SaveSkillLevelDto> dtos)
        {
            if (dtos == null)
            {
                return BadRequest(new Response<object> { Status = 400, Message = "Skill level data is required." });
            }

            var savedLevels = await _skillLevelService.SaveLevelsAsync(GetCurrentUserId(), dtos);
            return Ok(new Response<IEnumerable<SkillLevelDto>> 
            { 
                Status = 200, 
                Message = "Skill levels saved successfully.", 
                Data = savedLevels 
            });
        }
    }
}