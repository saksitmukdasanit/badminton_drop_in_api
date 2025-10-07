using DropInBadAPI.Data;
using DropInBadAPI.Dtos;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Services
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
        public async Task<IEnumerable<SkillLevelDto>> SaveLevelsAsync(int organizerUserId, IEnumerable<CreateSkillLevelDto> dtos)
        {
            // 1. ค้นหาระดับมือเก่าทั้งหมดของผู้ใช้นี้
            var existingLevels = await _context.OrganizerSkillLevels
                .Where(sl => sl.OrganizerUserId == organizerUserId)
                .ToListAsync();

            // 2. ลบของเก่าทิ้งทั้งหมด
            if (existingLevels.Any())
            {
                _context.OrganizerSkillLevels.RemoveRange(existingLevels);
            }

            // 3. เตรียมข้อมูลใหม่ทั้งหมดจาก DTO ที่ส่งมา
            var newLevels = dtos.Select(dto => new OrganizerSkillLevel
            {
                OrganizerUserId = organizerUserId,
                LevelRank = dto.LevelRank,
                LevelName = dto.LevelName,
                ColorHexCode = dto.ColorHexCode
            }).ToList();

            // 4. เพิ่มข้อมูลใหม่ทั้งหมดลง Context ในครั้งเดียว (ประสิทธิภาพดีกว่า Add ทีละอัน)
            await _context.OrganizerSkillLevels.AddRangeAsync(newLevels);

            // 5. บันทึกการเปลี่ยนแปลงทั้งหมดลงฐานข้อมูลใน Transaction เดียว
            await _context.SaveChangesAsync();

            // 6. แปลงผลลัพธ์กลับไปเป็น DTO เพื่อส่งคืน
            return newLevels.Select(l => new SkillLevelDto(l.SkillLevelId, l.LevelRank, l.LevelName, l.ColorHexCode));
        }

    }
}