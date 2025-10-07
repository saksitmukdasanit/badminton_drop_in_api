using DropInBadAPI.Data;
using DropInBadAPI.Interfaces;
using DropInBadAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DropInBadAPI.Repositories
{
    public class FacilityService : IFacilityService
    {
        private readonly BadmintonDbContext _context;

        public FacilityService(BadmintonDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Facility>> GetAllActiveAsync()
        {
            return await _context.Facilities
                                 .Where(f => f.IsActive == true)
                                 .ToListAsync();
        }

        public async Task<Facility?> GetByIdAsync(int id)
        {
            return await _context.Facilities.FindAsync(id);
        }

        public async Task<Facility> AddAsync(Facility facility)
        {
            // กำหนดค่าเริ่มต้นก่อนบันทึก
            facility.FacilityName = facility.FacilityName;
            facility.IconName = facility.IconName;
            facility.CreatedDate = DateTime.Now;
            facility.IsActive = facility.IsActive ?? true;

            await _context.Facilities.AddAsync(facility);
            await _context.SaveChangesAsync();
            return facility;
        }

        public async Task<Facility?> UpdateAsync(Facility facility)
        {
            var existingFacility = await _context.Facilities.FindAsync(facility.FacilityId);

            if (existingFacility == null)
            {
                return null; // ไม่เจอข้อมูลที่จะอัปเดต
            }

            // อัปเดตค่าที่ต้องการ
            existingFacility.FacilityName = facility.FacilityName;
            existingFacility.IconName = facility.IconName;
            existingFacility.IsActive = facility.IsActive;
            existingFacility.UpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync();
            return existingFacility;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var facilityToDelete = await _context.Facilities.FindAsync(id);

            if (facilityToDelete == null)
            {
                return false; // ไม่เจอข้อมูลที่จะลบ
            }

            // เราจะใช้วิธี "Soft Delete" คือไมลบข้อมูลทิ้งจริงๆ แต่เปลี่ยนสถานะเป็นไม่ใช้งาน
            // เพื่อรักษาข้อมูลในอดีตไว้
            facilityToDelete.IsActive = false;
            facilityToDelete.UpdatedDate = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}