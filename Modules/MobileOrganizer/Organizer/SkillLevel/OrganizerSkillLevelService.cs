using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Service.Mobile.Organizer
{
    public class OrganizerSkillLevelService : IOrganizerSkillLevelService
    {
        private readonly BadmintonDbContext _context;
        public OrganizerSkillLevelService(BadmintonDbContext context) { _context = context; }

        public async Task<IEnumerable<SkillLevelDto>> GetLevelsByOrganizerAsync(int organizerUserId)
        {
            return await _context.OrganizerSkillLevels
                .Where(sl => sl.OrganizerUserId == organizerUserId && sl.IsActive == true)
                .OrderBy(sl => sl.LevelRank)
                .Select(sl => new SkillLevelDto(sl.SkillLevelId, sl.LevelRank, sl.LevelName, sl.ColorHexCode))
                .ToListAsync();
        }

        public async Task<SkillLevelDto?> GetLevelByIdAsync(int skillLevelId, int organizerUserId)
        {
            return await _context.OrganizerSkillLevels
                .Where(sl => sl.SkillLevelId == skillLevelId && sl.OrganizerUserId == organizerUserId)
                .Select(sl => new SkillLevelDto(sl.SkillLevelId, sl.LevelRank, sl.LevelName, sl.ColorHexCode))
                .FirstOrDefaultAsync();
        }
        public async Task<IEnumerable<SkillLevelDto>> SaveLevelsAsync(int organizerUserId, IEnumerable<SaveSkillLevelDto> dtos)
        {
           var existingLevels = await _context.OrganizerSkillLevels
                .Where(sl => sl.OrganizerUserId == organizerUserId)
                .ToListAsync();

            // 2. แยก ID ของข้อมูลชุดใหม่ที่ส่งเข้ามา (เฉพาะอันที่มี ID)
            var incomingLevelIds = dtos
                .Where(d => d.SkillLevelId.HasValue)
                .Select(d => d.SkillLevelId!.Value)
                .ToHashSet(); // ToHashSet() เพื่อการค้นหาที่เร็วขึ้น

            // 3. จัดการรายการที่ต้อง "ลบ" (Soft Delete)
            // คือรายการที่เคยมีใน DB แต่ไม่ได้ถูกส่งมาในข้อมูลชุดใหม่
            var levelsToDelete = existingLevels.Where(l => !incomingLevelIds.Contains(l.SkillLevelId));
            foreach (var level in levelsToDelete)
            {
                level.IsActive = false;
            }

            // 4. จัดการรายการที่ต้อง "เพิ่ม" หรือ "แก้ไข"
            foreach (var dto in dtos)
            {
                if (dto.SkillLevelId.HasValue) // ถ้ามี ID มาด้วย = แก้ไข (Update)
                {
                    var levelToUpdate = existingLevels.FirstOrDefault(l => l.SkillLevelId == dto.SkillLevelId.Value);
                    if (levelToUpdate != null)
                    {
                        levelToUpdate.LevelRank = dto.LevelRank;
                        levelToUpdate.LevelName = dto.LevelName;
                        levelToUpdate.ColorHexCode = dto.ColorHexCode;
                        levelToUpdate.IsActive = true; // เผื่อเป็นการเปิดใช้งานรายการที่เคยลบไปแล้ว
                        levelToUpdate.UpdatedDate = DateTime.UtcNow;
                    }
                }
                else // ถ้าไม่มี ID = สร้างใหม่ (Add)
                {
                    var newLevel = new OrganizerSkillLevel
                    {
                        OrganizerUserId = organizerUserId,
                        LevelRank = dto.LevelRank,
                        LevelName = dto.LevelName,
                        ColorHexCode = dto.ColorHexCode,
                        IsActive = true,
                        CreatedDate = DateTime.UtcNow
                    };
                    await _context.OrganizerSkillLevels.AddAsync(newLevel);
                }
            }

            // 5. บันทึกการเปลี่ยนแปลงทั้งหมดลง DB ในครั้งเดียว
            await _context.SaveChangesAsync();

            // 6. ดึงข้อมูลล่าสุดทั้งหมดที่ Active อยู่ ส่งกลับไป
            return await GetLevelsByOrganizerAsync(organizerUserId);
        }
    }
}