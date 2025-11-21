using DropInBadAPI.Dtos;
using DropInBadAPI.Models;

namespace DropInBadAPI.Service.Mobile.Organizer
{
    public interface IOrganizerSkillLevelService
    {
        Task<IEnumerable<SkillLevelDto>> GetLevelsByOrganizerAsync(int organizerUserId);
        Task<SkillLevelDto?> GetLevelByIdAsync(int skillLevelId, int organizerUserId);
        Task<IEnumerable<SkillLevelDto>> SaveLevelsAsync(int organizerUserId, IEnumerable<SaveSkillLevelDto> dtos);

    }
}