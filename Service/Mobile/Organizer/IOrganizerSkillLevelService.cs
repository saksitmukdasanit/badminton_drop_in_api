using DropInBadAPI.Dtos;
using DropInBadAPI.Models;

namespace DropInBadAPI.Interfaces
{
    public interface IOrganizerSkillLevelService
    {
        Task<IEnumerable<SkillLevelDto>> GetLevelsByOrganizerAsync(int organizerUserId);
        Task<SkillLevelDto?> GetLevelByIdAsync(int skillLevelId, int organizerUserId);
        Task<IEnumerable<SkillLevelDto>> SaveLevelsAsync(int organizerUserId, IEnumerable<CreateSkillLevelDto> dtos);

    }
}